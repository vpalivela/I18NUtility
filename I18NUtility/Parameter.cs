using System;

namespace I18NUtility
{
    public class Parameter<T> : IParameter
    {
        private T _value;

        public Parameter(string name, string alternateName, bool isRequired)
        {
            Name = name;
            AlternateName = alternateName;
            IsRequired = isRequired;
        }

        public string Name { get; private set; }

        public string AlternateName { get; private set; }

        public Type ValueType
        {
            get { return typeof(T); }
        }

        public bool IsRequired { get; private set; }

        public bool HasValue { get; private set; }

        public T Value
        {
            get { return _value; }
            set
            {
                _value = value;
                HasValue = true;
            }
        }
    }
}