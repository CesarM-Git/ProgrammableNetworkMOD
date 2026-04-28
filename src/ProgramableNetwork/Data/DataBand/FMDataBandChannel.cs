using Mafi;
using Mafi.Core;
using Mafi.Core.Entities;
using Mafi.Serialization;
using Mafi.Unity.UiToolkit.Component;
using System;
using System.Linq;

namespace ProgramableNetwork;

public class FMDataBandChannel : IDataBandChannel {
	public int Index { get; set; }
	public string Id3 { get; set; }
	/// <summary>
	/// Pre-allocated signal data buffer. Only the first <see cref="ValueLength"/> elements are valid.
	/// Do NOT replace this array — write into it and update ValueLength instead.
	/// </summary>
	public Fix32[] Value { get; set; } = Array.Empty<Fix32>();
	/// <summary>
	/// Number of valid elements in <see cref="Value"/>. May be less than Value.Length.
	/// </summary>
	public int ValueLength { get; set; }
	public int ValidIterations { get; set; }
	public Antena Antena { get => m_antena; set { m_antenaId = value?.Id ?? new EntityId(0); m_antena = value; } }

	public FMDataBand OriginalDataBand { get; set; }

	private Antena m_antena;
	private EntityId m_antenaId;

	/// <summary>
	/// Copies <paramref name="source"/> into the stable <see cref="Value"/> buffer,
	/// growing the buffer only when necessary. Sets <see cref="ValueLength"/>.
	/// </summary>
	public void WriteValue(Fix32[] source, int length)
	{
		if (Value.Length < length)
		{
			Value = new Fix32[length];
		}
		Array.Copy(source, Value, length);
		ValueLength = length;
	}

	/// <summary>
	/// Marks the channel as having no valid data without reallocating the buffer.
	/// </summary>
	public void ClearValue()
	{
		ValueLength = 0;
	}

	public static void Serialize(FMDataBandChannel channel, BlobWriter writer) {
		writer.WriteByte(/*version*/4);
		writer.WriteInt(channel.Index);
		writer.WriteString(channel.Id3 ?? string.Empty);
		// Serialize only the valid portion of the buffer
		Fix32[] toWrite;
		if (channel.ValueLength == 0)
		{
			toWrite = Array.Empty<Fix32>();
		}
		else if (channel.ValueLength == channel.Value.Length)
		{
			toWrite = channel.Value;
		}
		else
		{
			toWrite = new Fix32[channel.ValueLength];
			Array.Copy(channel.Value, toWrite, channel.ValueLength);
		}
		writer.WriteArray(toWrite);
		writer.WriteInt(channel.ValidIterations);
		writer.WriteInt(channel.m_antenaId.Value);
	}

	public static FMDataBandChannel Deserialize(BlobReader reader) {
		var version = reader.ReadByte();
		int index = reader.ReadInt();
		string customName = version >= 4 ? reader.ReadString() : string.Empty;
		Fix32[] value;
		if (version < 3) {
			value = reader.ReadArray<int>().Select(Fix32.FromInt).ToArray();
		} else {
			value = reader.ReadArray<Fix32>();
		}
		return new FMDataBandChannel() {
			Index = index,
			Id3 = customName,
			Value = value,
			ValueLength = value.Length,
			ValidIterations = reader.ReadInt(),
			m_antenaId = new EntityId(version > 0 ? reader.ReadInt() : 0)
		};
	}

	public void UpdateAntenaReference(FMDataBand self, IEntitiesManager manager) {
		OriginalDataBand = self;
		manager.TryGetEntity(m_antenaId, out m_antena);
	}

	public void Update() {
		if (Antena?.DataBand is FMDataBand targetDataBand) {
			(Fix32[] data, int length) = targetDataBand.Read(Index);
			OriginalDataBand.Update(Index, data, length);
			OriginalDataBand.Id3(Index, targetDataBand.GetId3(Index));
		}
	}

	public UiComponent CreateUI(Ui.AntenaInspector antenaInspector, IDataBandChannel channel) {
		return OriginalDataBand.Prototype.Buttons(antenaInspector, channel);
	}

	public void Move(int v) {
		int newIndex = Index + v;
		if (newIndex < 0) {
			Index = OriginalDataBand.Prototype.Channels + newIndex;
		} else if (newIndex >= OriginalDataBand.Prototype.Channels) {
			Index = newIndex - OriginalDataBand.Prototype.Channels;
		} else {
			Index = newIndex;
		}
	}
}