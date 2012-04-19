using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using I18NUtility.Properties;

namespace I18NUtility.Commands
{
    class ImportResxCommand : Command
    {
        private const string _CommandName = "ImportResx";
        private const string _ParameterNameSource = "Source";
        private const string _ParameterAlternateNameSource = "S";
        private const string _ParameterNameTarget = "Target";
        private const string _ParameterAlternateNameTarget = "T";

        private int _importedFileCount;

        protected internal ImportResxCommand()
            : base(_CommandName, Resources.ImportResxCommandUsage, Resources.ImportResxCommandExamples)
        {
            Parameters.Add(new Parameter<string>(_ParameterNameSource, _ParameterAlternateNameSource, true));
            Parameters.Add(new Parameter<string>(_ParameterNameTarget, _ParameterAlternateNameTarget, true));
        }

        public override bool ParseParameterValue(IParameter parameter, string parameterValue)
        {
            bool returnValue = base.ParseParameterValue(parameter, parameterValue);

            if (returnValue)
            {
                switch (parameter.Name)
                {
                    case _ParameterAlternateNameSource:
                    case _ParameterNameSource:
                        parameterValue = parameterValue.Trim();
                        if (Directory.Exists(parameterValue))
                        {
                            ((Parameter<string>)parameter).Value = parameterValue;
                        }
                        else
                        {
                            returnValue = false;
                        }
                        break;
                }
            }

            return returnValue;
        }

        public override Command CreateCommand()
        {
            return new ImportResxCommand();
        }

        public override bool Execute(out string failureReasonMessage)
        {
            string source = GetParameter<string>(_ParameterNameSource).Value;
            string target = GetParameter<string>(_ParameterNameTarget).Value.Trim();

            if (Directory.Exists(source))
            {
                // Export directory:
                ImportDirectory(source, target);
                Console.WriteLine(Resources.ImportResxFilesImported, _importedFileCount);
            }
            failureReasonMessage = null;
            return true;
        }

        private void ImportDirectory(string source, string target)
        {
            var sourceResxFiles = Directory.GetFiles(source, "*.*.resx", SearchOption.TopDirectoryOnly);

            // Create target directory only if the corresponding source contains resx files
            if (sourceResxFiles.Any())
            {
                if (!Directory.Exists(target))
                {
                    Directory.CreateDirectory(target);
                }
            }

            // Import the RESX files
            foreach (var sourceResxFile in sourceResxFiles)
            {
                var resxDataNodes = ReadResxFile(sourceResxFile);

                var targetResxFile = sourceResxFile.Replace(source, target);

                var writer = new ResXResourceWriter(targetResxFile);
                foreach (var resxDataNode in resxDataNodes)
                {
                    writer.AddResource(resxDataNode);
                }
                writer.Generate();
                _importedFileCount++;
            }

            foreach (string subFolder in Directory.GetDirectories(source))
            {
                ImportDirectory(subFolder, subFolder.Replace(source, target));
            }
        }

        private IList<ResXDataNode> ReadResxFile(string sourceResxFile)
        {
            var resXDataNodes = new List<ResXDataNode>();
            var reader = new ResXResourceReader(sourceResxFile) { UseResXDataNodes = true };

            // Add this to get relative path for embedded images.
            if (sourceResxFile.Contains("Properties\\Resources.resx"))
            {
                reader.BasePath = sourceResxFile.Replace("Resources.resx", "");
            }

            try
            {
                resXDataNodes.AddRange(from DictionaryEntry entry in reader select entry.Value as ResXDataNode);
            }
            finally
            {
                reader.Close();
            }

            return resXDataNodes;
        }
    }
}
