using System;

namespace I18NUtility
{
    public interface IParameter
    {
        string Name { get; }
        string AlternateName { get; }
        Type ValueType { get; }
        bool IsRequired { get; }
        bool HasValue { get; }
    }
}
