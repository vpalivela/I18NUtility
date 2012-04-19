using System;
using System.IO;
using System.Linq;
using I18NUtility.Properties;

namespace I18NUtility.Commands
{
    class ExportResxCommand : Command
    {
        private const string _CommandName = "ExportResx";
        private const string _ParameterNameSource = "Source";
        private const string _ParameterAlternateNameSource = "S";
        private const string _ParameterNameTarget = "Target";
        private const string _ParameterAlternateNameTarget = "T";

        private int _exportedFileCount;


        protected internal ExportResxCommand()
            : base(_CommandName, Resources.ExportResxCommandUsage, Resources.ExportResxCommandExamples)
        {
            Parameters.Add(new Parameter<string>(_ParameterNameSource, _ParameterAlternateNameSource, true));
            Parameters.Add(new Parameter<string>(_ParameterNameTarget, _ParameterAlternateNameTarget, true));
        }

        public override Command CreateCommand()
        {
            return new ExportResxCommand();
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

        public override bool Execute(out string failureReasonMessage)
        {
            string source = GetParameter<string>(_ParameterNameSource).Value;
            string target = GetParameter<string>(_ParameterNameTarget).Value.Trim();

            if (Directory.Exists(source))
            {
                // Empty contents of target directory:
                if (Directory.Exists(target))
                {
                    DeleteDirectoryContents(target);
                }

                // Export directory:
                ExportDirectory(source, target);
                Console.WriteLine(Resources.ExportResxFilesExported, _exportedFileCount);
            }
            failureReasonMessage = null;
            return true;
        }

        private void ExportDirectory(string source, string target)
        {
            var sourceResxFiles = Directory.GetFiles(source, "*.resx", SearchOption.TopDirectoryOnly);

            // Create target directory only if the corresponding source contains resx files
            if (sourceResxFiles.Any())
            {
                if (!Directory.Exists(target))
                {
                    Directory.CreateDirectory(target);
                }
            }

            // Export the RESX files
            foreach (var sourceResxFile in sourceResxFiles)
            {
                var targetResxFile = sourceResxFile.Replace(source, target);
                File.Copy(sourceResxFile, targetResxFile, true);
                _exportedFileCount++;
            }

            foreach (string subFolder in Directory.GetDirectories(source))
            {
                ExportDirectory(subFolder, subFolder.Replace(source, target));
            }
        }

        private void DeleteDirectoryContents(string sDir)
        {
            var downloadedMessageInfo = new DirectoryInfo(sDir);

            foreach (FileInfo file in downloadedMessageInfo.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in downloadedMessageInfo.GetDirectories())
            {
                dir.Delete(true);
            }
        }
    }
}
