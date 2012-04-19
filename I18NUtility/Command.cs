using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace I18NUtility
{
    public abstract class Command
    {
        private Collection<IParameter> _parameters = new Collection<IParameter>();

        protected Command(string name, string usage, string examples)
        {
            Name = name;
            Usage = usage;
            Examples = examples;
        }

        public abstract Command CreateCommand();

        // Command name should be no longer than 20 characters
        public string Name { get; private set; }

        // Command usage should be formatted as a block of text with lines no longer than 60 characters
        public string Usage { get; private set; }

        // Command examples should be formatted as a block of text with lines no longer than 80 characters.
        // It's okay to leave examples null or string.Empty.
        public string Examples { get; private set; }

        public ICollection<IParameter> Parameters
        {
            get { return _parameters; }
        }

        public void SetParameterValue(string parameterName, string parameterValue)
        {
            string rawParameterName = parameterName;
            parameterName = parameterName.Substring(1);

            bool wasParameterProcessed = false;
            foreach (IParameter parameter in _parameters)
            {
                if (parameter.Name.ToUpper() == parameterName.ToUpper() || parameter.AlternateName.ToUpper() == parameterName.ToUpper())
                {
                    if (parameter.HasValue)
                        throw new Exception(string.Format(Properties.Resources.DuplicateParameter, rawParameterName));

                    if (!ParseParameterValue(parameter, parameterValue))
                        throw new Exception(string.Format(Properties.Resources.InvalidParameter, new object[] { rawParameterName, parameterValue }));

                    wasParameterProcessed = true;

                    break;
                }
            }

            if (!wasParameterProcessed)
                throw new Exception(string.Format(Properties.Resources.InvalidParameter, new object[] { rawParameterName, parameterValue }));
        }

        public virtual bool ParseParameterValue(IParameter parameter, string parameterValue)
        {
            if (parameter.ValueType == typeof(string))
            {
                string parsedParameterValue = null;

                if (!string.IsNullOrEmpty(parameterValue))
                    parsedParameterValue = parameterValue.Trim();

                ((Parameter<string>)parameter).Value = parsedParameterValue;

                return true;
            }
            
            if (parameter.ValueType == typeof(bool))
            {
                bool parsedParameterValue;
                if (bool.TryParse(parameterValue, out parsedParameterValue))
                {
                    ((Parameter<bool>)parameter).Value = parsedParameterValue;
                    return true;
                }
                return false;
            }

            if (parameter.ValueType == typeof(int))
            {
                int parsedParameterValue;
                if (int.TryParse(parameterValue, out parsedParameterValue))
                {
                    ((Parameter<int>)parameter).Value = parsedParameterValue;
                    return true;
                }
                return false;
            }
            return false;
        }

        public virtual bool IsValid(out string invalidReason)
        {
            bool returnValue = true;
            invalidReason = null;

            foreach (IParameter parameter in _parameters.Where(parameter => parameter.IsRequired && !parameter.HasValue))
            {
                invalidReason = string.Format(Properties.Resources.ParameterRequired, new object[] { Name, parameter.Name });
                returnValue = false;
            }

            return returnValue;
        }

        public Parameter<T> GetParameter<T>(string parameterName)
        {
            return _parameters.Where(parameter => parameter.Name == parameterName).Cast<Parameter<T>>().FirstOrDefault();
        }

        public abstract bool Execute(out string failureReasonMessage);
    }
}
