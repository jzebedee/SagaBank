using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SagaBank.Shared;

//from https://github.com/Cysharp/Ulid
public class UlidToBytesConverter : ValueConverter<Ulid, byte[]>
{
    private static readonly ConverterMappingHints defaultHints = new ConverterMappingHints(size: 16);

    //shouldn't be needed, but otherwise we get MME for not having a parameterless ctor
    public UlidToBytesConverter() : this(mappingHints: default) { }

    public UlidToBytesConverter(ConverterMappingHints mappingHints = null)
        : base(
                convertToProviderExpression: x => x.ToByteArray(),
                convertFromProviderExpression: x => new Ulid(x),
                mappingHints: defaultHints.With(mappingHints))
    {
    }
}
