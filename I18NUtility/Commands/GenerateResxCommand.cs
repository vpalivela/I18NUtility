using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using I18NUtility.Properties;

namespace I18NUtility.Commands
{
    public class GenerateResxCommand : Command
    {
        private const string _CommandName = "GenerateResx";
        private const string _ParameterNamePath = "Path";
        private const string _ParameterAlternateNamePath = "P";
        private const string _ParameterNameCultureCode = "CultureCode";
        private const string _ParameterAlternateNameCultureCode = "CC";
        private const string _PseudoCode = "ja";

        private int _generatedFileCount;
        private int _generatedResourcesCount;
        private int _foundResourceWordsCount;
        private int _foundResourcesCount;

        private readonly List<string> _emptyResourceFiles = new List<string>();
        
        protected internal GenerateResxCommand()
            : base(_CommandName, Resources.GenerateResxCommandUsage, Resources.GenerateResxCommandExamples)
        {
            Parameters.Add(new Parameter<string>(_ParameterNamePath, _ParameterAlternateNamePath, true));
            Parameters.Add(new Parameter<string>(_ParameterNameCultureCode, _ParameterAlternateNameCultureCode, true));
        }

        public override Command CreateCommand()
        {
            return new GenerateResxCommand();
        }

        public override bool ParseParameterValue(IParameter parameter, string parameterValue)
        {
            bool returnValue = base.ParseParameterValue(parameter, parameterValue);

            if (returnValue)
            {
                switch (parameter.Name)
                {
                    case _ParameterNamePath:
                    case _ParameterAlternateNamePath:
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
            string path = GetParameter<string>(_ParameterNamePath).Value;
            string cultureCode = GetParameter<string>(_ParameterNameCultureCode).Value.ToLower();

            if (Directory.Exists(path))
            {
                var invariantFiles = GetInvariantFiles(path);

                foreach (var invariantFile in invariantFiles)
                {
                    GenerateResx(invariantFile, cultureCode, cultureCode.Equals(_PseudoCode, StringComparison.InvariantCultureIgnoreCase));
                }

                if (_emptyResourceFiles.Any())
                {
                    Console.WriteLine(Resources.GenerateResxNoResourcesWarning);

                    foreach (var emptyResourceFile in _emptyResourceFiles)
                    {
                        Console.WriteLine("\t{0}", emptyResourceFile);
                    }
                    Console.WriteLine();
                }
                Console.WriteLine(Resources.GenerateResxInvariantFilesFound, invariantFiles.Count);
                Console.WriteLine(Resources.GenerateResxResourcesFound, _foundResourcesCount);
                Console.WriteLine(Resources.GenerateResxWordsFound, _foundResourceWordsCount);
                Console.WriteLine(Resources.GenerateResxEmptyFilesFound, _emptyResourceFiles.Count);
                Console.WriteLine(Resources.GenerateResxFilesGenerated, _generatedFileCount);
                Console.WriteLine(Resources.GenerateResxResourcesGenerated, _generatedResourcesCount);
            }

            failureReasonMessage = null;
            return true;
        }

        private void GenerateResx(string invariantFile, string cultureCode, bool isPseudo)
        {
            const bool xDataNodes = true;

            string fileSaveName = invariantFile.Replace(".resx", string.Format(".{0}.resx", cultureCode));

            // Open the input file.
            ResXResourceReader reader = new ResXResourceReader(invariantFile) { UseResXDataNodes = xDataNodes };

            // Add this to get relative path for embedded images.
            if (invariantFile.Contains("Properties\\Resources.resx"))
            {
                reader.BasePath = invariantFile.Replace("Resources.resx", "");
            }

            try
            {
                // Allocate the list for this instance.
                var textResourcesList = new SortedList();

                // Run through the file looking for only true text related
                // properties and only those with values set.
                foreach (DictionaryEntry dic in reader)
                {
                    string key = (string)dic.Key;
                    //removed key.StartsWith("$")
                    if (key.StartsWith(">>") || string.IsNullOrWhiteSpace(key))
                        continue;

                    ResXDataNode dataNode = dic.Value as ResXDataNode;
                    if (dataNode == null)
                        continue;
                    if (dataNode.FileRef != null)
                        continue;

                    string valueType = dataNode.GetValueTypeName((ITypeResolutionService)null);
                    if (!valueType.StartsWith("System.String, "))
                        continue;

                    if (dataNode.Comment.ToLower().Contains("Do not translate".ToLower()))
                        continue;

                    object valueObject = dataNode.GetValue((ITypeResolutionService)null);
                    string value = valueObject == null ? "" : valueObject.ToString();

                    _foundResourcesCount++;
                    _foundResourceWordsCount += Regex.Matches(value, @"[\S]+").Count;
                    textResourcesList.Add(dataNode.Name, value);
                }

                // It's entirely possible that there are no text strings in the
                // .ResX file.
                if (textResourcesList.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(fileSaveName))
                    {
                        // Create the new file.
                        var writer = new ResXResourceWriter(fileSaveName);

                        foreach (DictionaryEntry textdic in textResourcesList)
                        {
                            writer.AddResource(textdic.Key.ToString(),
                                               isPseudo ? PseudoTranslate(textdic.Value.ToString()) : string.Empty);

                        }

                        writer.Generate();
                        writer.Close();
                        _generatedFileCount++;
                        _generatedResourcesCount += textResourcesList.Count;
                    }
                }
                else
                {
                    _emptyResourceFiles.Add(invariantFile);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(Resources.GenerateResxError, invariantFile);
                Console.WriteLine(e.Message);
            }
            finally
            {
                reader.Close();
            }
        }

        /// <summary>
        /// Converts a string to a pseudo internationized string.
        /// </summary>
        /// <remarks>
        /// Primarily for latin based languages.  This will need updating to
        /// work with Eastern languages.
        /// </remarks>
        /// <param name="inputString">
        /// The string to use as a base.
        /// </param>
        /// <returns>
        /// A longer and twiddled string.
        /// </returns>
        private string PseudoTranslate(string inputString)
        {
            // Calculate the extra space necessary for pseudo
            // internationalization.  The rules, according to "Developing
            // International Software" is that < 10  characters you should grow
            // by 400% while >= 10 characters should grow by 30%.

            int OrigLen = inputString.Length;
            int PseudoLen = 0;
            if (OrigLen < 10)
            {
                PseudoLen = (OrigLen * 4) + OrigLen;
            }
            else
            {
                PseudoLen = ((int)(OrigLen * 0.3)) + OrigLen;
            }

            StringBuilder sb = new StringBuilder(PseudoLen);

            // The pseudo string will always start with a "[" and end
            // with a "]" so you can tell if strings are not built
            // correctly in the UI.
            sb.Append("[ !!! ");

            bool waitingForEndBrace = false;
            bool waitingForGreaterThan = false;
            foreach (Char currChar in inputString)
            {
                switch (currChar)
                {
                    case '{':
                        waitingForEndBrace = true;
                        break;
                    case '}':
                        waitingForEndBrace = false;
                        break;
                    case '<':
                        waitingForGreaterThan = true;
                        break;
                    case '>':
                        waitingForGreaterThan = false;
                        break;
                }
                if (waitingForEndBrace || waitingForGreaterThan)
                {
                    sb.Append(currChar);
                    continue;
                }
                switch (currChar)
                {
                    case 'A':
                        sb.Append('Å');
                        break;
                    case 'B':
                        sb.Append('ß');
                        break;
                    case 'C':
                        sb.Append('C');
                        break;
                    case 'D':
                        sb.Append('Đ');
                        break;
                    case 'E':
                        sb.Append('Ē');
                        break;
                    case 'F':
                        sb.Append('F');
                        break;
                    case 'G':
                        sb.Append('Ğ');
                        break;
                    case 'H':
                        sb.Append('Ħ');
                        break;
                    case 'I':
                        sb.Append('Ĩ');
                        break;
                    case 'J':
                        sb.Append('Ĵ');
                        break;
                    case 'K':
                        sb.Append('Ķ');
                        break;
                    case 'L':
                        sb.Append('Ŀ');
                        break;
                    case 'M':
                        sb.Append('M');
                        break;
                    case 'N':
                        sb.Append('Ń');
                        break;
                    case 'O':
                        sb.Append('Ø');
                        break;
                    case 'P':
                        sb.Append('P');
                        break;
                    case 'Q':
                        sb.Append('Q');
                        break;
                    case 'R':
                        sb.Append('Ŗ');
                        break;
                    case 'S':
                        sb.Append('Ŝ');
                        break;
                    case 'T':
                        sb.Append('Ŧ');
                        break;
                    case 'U':
                        sb.Append('Ů');
                        break;
                    case 'V':
                        sb.Append('V');
                        break;
                    case 'W':
                        sb.Append('Ŵ');
                        break;
                    case 'X':
                        sb.Append('X');
                        break;
                    case 'Y':
                        sb.Append('Ÿ');
                        break;
                    case 'Z':
                        sb.Append('Ż');
                        break;


                    case 'a':
                        sb.Append('ä');
                        break;
                    case 'b':
                        sb.Append('þ');
                        break;
                    case 'c':
                        sb.Append('č');
                        break;
                    case 'd':
                        sb.Append('đ');
                        break;
                    case 'e':
                        sb.Append('ę');
                        break;
                    case 'f':
                        sb.Append('ƒ');
                        break;
                    case 'g':
                        sb.Append('ģ');
                        break;
                    case 'h':
                        sb.Append('ĥ');
                        break;
                    case 'i':
                        sb.Append('į');
                        break;
                    case 'j':
                        sb.Append('ĵ');
                        break;
                    case 'k':
                        sb.Append('ĸ');
                        break;
                    case 'l':
                        sb.Append('ľ');
                        break;
                    case 'm':
                        sb.Append('m');
                        break;
                    case 'n':
                        sb.Append('ŉ');
                        break;
                    case 'o':
                        sb.Append('ő');
                        break;
                    case 'p':
                        sb.Append('p');
                        break;
                    case 'q':
                        sb.Append('q');
                        break;
                    case 'r':
                        sb.Append('ř');
                        break;
                    case 's':
                        sb.Append('ş');
                        break;
                    case 't':
                        sb.Append('ŧ');
                        break;
                    case 'u':
                        sb.Append('ū');
                        break;
                    case 'v':
                        sb.Append('v');
                        break;
                    case 'w':
                        sb.Append('ŵ');
                        break;
                    case 'x':
                        sb.Append('χ');
                        break;
                    case 'y':
                        sb.Append('y');
                        break;
                    case 'z':
                        sb.Append('ž');
                        break;
                    default:
                        sb.Append(currChar);
                        break;
                }
            }

            // Poke on extra text to fill out the string.
            const String PadStr = " !!!";
            int PadCount = (PseudoLen - OrigLen - 2) / PadStr.Length;
            if (PadCount < 2)
            {
                PadCount = 2;
            }

            for (int x = 0; x < PadCount; x++)
            {
                sb.Append(PadStr);
            }

            // Pop on the trailing "]"
            sb.Append("]");

            return (sb.ToString());
        }

        IList<string> GetInvariantFiles(string sDir)
        {
            try
            {
                var temp = Directory.GetFiles(sDir, "*.*.resx", SearchOption.AllDirectories);
                return Directory.GetFiles(sDir, "*.resx", SearchOption.AllDirectories).Where(name => !temp.Contains(name)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }
    }
}
