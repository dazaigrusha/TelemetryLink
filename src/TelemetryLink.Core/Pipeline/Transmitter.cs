using TelemetryLink.Core.Compression;
using TelemetryLink.Core.Configuration;
using TelemetryLink.Core.Domain;
using TelemetryLink.Core.Format;
using TelemetryLink.Core.Security;

namespace TelemetryLink.Core.Pipeline;

/// <summary>Вікно → фіксована таблиця → вибір кадрів → кодування → захищений пакет.</summary>
public sealed class Transmitter
{
    private readonly TeamConfig _config;
    private readonly PipelineOptions _options;
    private readonly SessionKeys _keys;
    private readonly byte[] _salt;
    private readonly byte[] _noncePrefix;

    public Transmitter(TeamConfig config, PipelineOptions options, SessionKeys keys, byte[] salt, byte[] noncePrefix)
    {
        _config = config;
        _options = options;
        _keys = keys;
        _salt = salt;
        _noncePrefix = noncePrefix;
    }

    public FrameClass[] LastClasses { get; private set; } = [];

    public IReadOnlyList<Packet> BuildPackets(IReadOnlyList<TelemetryFrame> frames)
    {
        var tuning = _options.Tuning;
        if (tuning.WindowFrames < 1 || tuning.WindowFrames > ushort.MaxValue
            || !double.IsFinite(tuning.CompressionThreshold) || tuning.CompressionThreshold < 0
            || !double.IsFinite(tuning.CriticalWeight) || tuning.CriticalWeight < 0)
            throw new ArgumentException("Некоректні параметри вікна, порогу або ваги critical.");
        int payloadLimit = Math.Min(_config.MaxPayloadBytes, _config.MaxPacketBytes - PacketWriter.MinimumPacketSize);
        if (payloadLimit < PayloadContent.FixedFieldsSize)
            throw new ArgumentException("Ліміт пакета не вміщує навіть порожнє навантаження.");
        if (frames.Any(frame => !frame.IsValid)) throw new ArgumentException("Некоректні поля телеметрії.", nameof(frames));
        LastClasses = _options.Classifier.Classify(frames);
        if (LastClasses.Length != frames.Count || LastClasses.Any(c => (int)c is < 0 or > 2))
            throw new ArgumentException("Класифікатор має повернути один клас 0..2 для кожного кадру.");
        int[] values = frames.Select((frame, i) => checked(_config.ValueOf(LastClasses[i], false)
            + (frame.IsCritical ? (int)Math.Round(_config.CriticalBonus * tuning.CriticalWeight,
                MidpointRounding.AwayFromZero) : 0))).ToArray();
        var packets = new List<Packet>();
        var rawCodec = new NoCompressionCodec();
        if (frames.Count == 0)
        {
            AddPacket([], rawCodec, CodeTable.Empty);
            return packets;
        }

        var pending = Enumerable.Range(0, frames.Count).ToList();
        while (pending.Count > 0)
        {
            int[] window = pending.Take(tuning.WindowFrames).ToArray();
            byte[] sample = TelemetryFrame.Serialize(window.Select(i => frames[i]).ToArray());
            // Таблиця будується за ВСІМ вікном і не змінюється після вибору.
            CodeTable table = _options.Codec.BuildTable(sample);
            bool rawRequested = (_options.Codec.Flags & PacketFlags.NoCompression) != 0;
            int[] rawSelection = Select(rawCodec, CodeTable.Empty, window, sample);
            int[] chosen = rawSelection;
            ISymbolCodec codec = rawCodec;
            CodeTable chosenTable = CodeTable.Empty;
            if (!rawRequested)
            {
                int[] compressedSelection = Select(_options.Codec, table, window, sample);
                // Поріг — відношення кількості кадрів, які вміщуються у два режими.
                if (compressedSelection.Length > 0 && (rawSelection.Length == 0
                    || compressedSelection.Length >= rawSelection.Length * tuning.CompressionThreshold))
                {
                    chosen = compressedSelection;
                    codec = _options.Codec;
                    chosenTable = table;
                }
            }
            if (chosen.Length == 0)
            {
                // Навіть нетиповий пакувальник не може залишити цикл без прогресу.
                if (payloadLimit < PayloadContent.FixedFieldsSize + TelemetryFrame.SizeInBytes)
                    throw new ArgumentException("Жоден кадр не вміщується у заданий ліміт пакета.");
                chosen = [0];
            }
            int[] indexes = chosen.Select(i => window[i]).ToArray();
            AddPacket(indexes, codec, chosenTable);
            var removed = indexes.ToHashSet();
            pending.RemoveAll(removed.Contains);
        }
        return packets;

        int[] Select(ISymbolCodec codec, CodeTable table, int[] window, byte[] sample)
        {
            long availableBits = 8L * (payloadLimit - PayloadContent.FixedFieldsSize - table.SizeInBytes);
            if (availableBits <= 0) return [];
            int capacity = (int)Math.Min(availableBits, int.MaxValue);
            bool raw = (codec.Flags & PacketFlags.NoCompression) != 0;
            var candidates = window.Select((index, i) => new FrameCandidate(i,
                raw ? 96 : table.MeasureBits(sample.AsSpan(i * 12, 12)), values[index])).ToArray();
            if (candidates.Any(c => c.WeightBits <= 0)) throw new InvalidOperationException("Некоректна вага кадру.");
            int[] selected = _options.Packer.Select(candidates, capacity).ToArray();
            if (selected.Any(i => i < 0 || i >= window.Length)
                || !selected.SequenceEqual(selected.Distinct().OrderBy(i => i))
                || selected.Sum(i => (long)candidates[i].WeightBits) > capacity)
                throw new InvalidOperationException("Пакувальник повернув некоректний набір індексів або перевищив місткість.");
            return selected;
        }

        void AddPacket(int[] indexes, ISymbolCodec codec, CodeTable table)
        {
            byte[] raw = TelemetryFrame.Serialize(indexes.Select(i => frames[i]).ToArray());
            byte[] encoded = codec.Encode(raw, table);
            uint sequence = checked((uint)packets.Count);
            byte[] bytes = PacketWriter.Build(new PayloadContent(indexes.Length, codec.Flags, table,
                raw.Length, encoded), _options.DeviceId, sequence, _salt,
                PacketCipher.BuildNonce(_noncePrefix, sequence), _keys);
            if (bytes.Length > _config.MaxPacketBytes || bytes.Length - PacketWriter.MinimumPacketSize > payloadLimit)
                throw new InvalidOperationException("Сформований пакет перевищує ліміт.");
            packets.Add(new Packet(sequence, bytes, indexes, checked(indexes.Sum(i => values[i]))));
        }
    }
}
