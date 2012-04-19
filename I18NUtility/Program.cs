using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace I18NUtility
{
    internal class Program
    {
        private const string _ParameterNameDebug = "Debug";
        private const string _ParameterNameHelp = "Help";
        private const string _ParameterNameAlternateHelp = "H";
        private const string _ParameterNameAlternate2Help = "?";

        private const bool _allowOnlyOneCommandPerExecution = true;
        private static readonly Collection<Command> _allCommands = new Collection<Command>();
        private static readonly Collection<Command> _commands = new Collection<Command>();

        /// <summary>
        /// Entry point
        /// </summary>
        /// <param name="args"></param>
        /// <returns>int containing 0 if successful, 1 otherwise</returns>
        public static int Main(string[] args)
        {
            try
            {
                bool inDebugMode;
                string[] arguments = PrepareArguments(args, out inDebugMode);

                if (inDebugMode)
                {
                    Console.Write(Properties.Resources.AttachDebugger);
                    Console.ReadLine();
                    // Set breakpoint(s) anywhere below this line
                }

                PopulateAllCommandsCollection();

                if (arguments.Length == 0 || IsHelpParameter(arguments[0]))
                {
                    ShowUsage();
                }
                else
                {
                    ParseArguments(arguments);

                    ValidateCommands();

                    ExecuteCommands();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(string.Format(Properties.Resources.AnErrorOccurred, ex.Message));

                return 1;
            }
        }

        private static void PopulateAllCommandsCollection()
        {
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof (Command))))
            {
                _allCommands.Add((Command)Activator.CreateInstance(type, true));
            }
        }

        private static bool IsHelpParameter(string parameter)
        {
            parameter = parameter.Trim();
            if (!parameter.StartsWith("-") && !parameter.StartsWith("/"))
            {
                return false;
            }
            
            parameter = parameter.Substring(1).ToUpper();

            return (parameter == _ParameterNameHelp.ToUpper() || parameter == _ParameterNameAlternateHelp.ToUpper() ||
                    parameter == _ParameterNameAlternate2Help.ToUpper());
        }

        private static string[] PrepareArguments(string[] args, out bool inDebugMode)
        {
            inDebugMode = false;
            string[] returnValue = null;
            if (args.Length > 0)
            {
                string argumentName = args[0].ToUpper();
                if (argumentName.StartsWith("-") || argumentName.StartsWith("/"))
                    argumentName = argumentName.Substring(1);

                if (argumentName == _ParameterNameDebug.ToUpper())
                {
                    returnValue = new string[args.Length - 1];
                    Array.Copy(args, 1, returnValue, 0, args.Length - 1);

                    inDebugMode = true;
                }
            }

            if (!inDebugMode)
            {
                returnValue = new string[args.Length];
                Array.Copy(args, returnValue, args.Length);
            }

            return returnValue;
        }

        private static void ShowUsage()
        {
            var currentAssembly = Assembly.GetExecutingAssembly().GetName();

            var usage = new StringBuilder(string.Format(Properties.Resources.Usage, new object[] { currentAssembly.Name, currentAssembly.Version.ToString() }));
            var examples = new StringBuilder();

            foreach (Command command in _allCommands)
            {
                if (!string.IsNullOrEmpty(command.Usage))
                {
                    usage.AppendLine(FormatCommandUsage(command));
                }

                if (string.IsNullOrEmpty(command.Examples)) continue;
                
                examples.AppendLine(string.Format(command.Examples, currentAssembly.Name));
                examples.AppendLine();
            }
            usage.AppendLine();
            if (examples.Length > 0)
            {
                usage.AppendLine(Properties.Resources.Examples);
                usage.AppendLine(examples.ToString());
            }

            Console.WriteLine();
            Console.WriteLine(usage.ToString());
        }

        private static string FormatCommandUsage(Command command)
        {
            var usage = new StringBuilder();

            usage.Append(command.Name);

            string[] usageLines = command.Usage.Split('\n');
            var isFirstLine = true;
            foreach (string usageLine in usageLines)
            {
                usage.Append(isFirstLine ? new string(' ', 20 - command.Name.Length) : new string(' ', 20));
                isFirstLine = false;

                usage.AppendLine(usageLine);
            }

            return usage.ToString();
        }

        private static void ParseArguments(string[] arguments)
        {
            Command currentCommand = null;

            var argumentsQueue = new Queue<string>(arguments);

            while (argumentsQueue.Count > 0)
            {
                ProcessNextArgument(argumentsQueue, ref currentCommand);
            }
        }

        private static void ProcessNextArgument(Queue<string> argumentsQueue, ref Command currentCommand)
        {
            if (argumentsQueue.Count <= 0)
                return;
            
            string currentArgument = argumentsQueue.Dequeue();
            try
            {
                if (IsNewArgument(currentArgument))
                {
                    if (currentCommand == null)
                        throw new InvalidOperationException();

                    string argumentValue = null;
                    if (argumentsQueue.Count > 0 && !IsNewArgument(argumentsQueue.Peek()))
                        argumentValue = argumentsQueue.Dequeue();

                    currentCommand.SetParameterValue(currentArgument, argumentValue);
                }
                else
                {
                    currentCommand = CreateCommand(currentArgument);
                }
            }
            catch (InvalidOperationException)
            {
                throw new Exception(string.Format(Properties.Resources.InvalidSyntax, currentArgument));
            }
        }

        private static bool IsNewArgument(string argument)
        {
            return (argument.Trim().StartsWith("-") || argument.Trim().StartsWith("/"));
        }

        private static Command CreateCommand(string commandName)
        {
            Command returnValue = null;

            if (_allowOnlyOneCommandPerExecution && _commands.Count > 0)
            {
                throw new Exception(Properties.Resources.OnlyOneCommandAllowed);
            }
            
            foreach (Command command in _allCommands.Where(command => command.Name.Trim().ToUpper() == commandName.Trim().ToUpper()))
            {
                returnValue = command.CreateCommand();
            }
            if (returnValue == null)
                throw new Exception(string.Format(Properties.Resources.InvalidCommand, commandName));

            _commands.Add(returnValue);

            return returnValue;
        }

        private static void ValidateCommands()
        {
            if (_commands.Count == 0)
            {
                throw new Exception(Properties.Resources.CommandRequired);
            }
            
            foreach (Command command in _commands)
            {
                string invalidReason;
                if (!command.IsValid(out invalidReason))
                    throw new Exception(invalidReason);
            }
        }

        private static void ExecuteCommands()
        {
            foreach (Command command in _commands)
            {
                Console.WriteLine();
                Console.WriteLine(string.Format(Properties.Resources.ProcessingCommand, command.Name));
                Console.WriteLine("------------------------------------------------------------");

                var stopWatch = Stopwatch.StartNew();
                string failureReasonMessage;
                bool success = command.Execute(out failureReasonMessage);
                stopWatch.Stop();
                Console.WriteLine("Duration: {0} seconds", stopWatch.ElapsedMilliseconds / 1000.0);

                Console.WriteLine("------------------------------------------------------------");
                Console.Write(string.Format(Properties.Resources.ProcessingCommand, command.Name));
                Console.WriteLine(success ? Properties.Resources.Succeeded : string.Format(Properties.Resources.Failed, failureReasonMessage));
            }
        }
    }
}
