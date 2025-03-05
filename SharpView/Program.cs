using SharpView.Enums;
using SharpView.Returns;
using SharpView.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Security;

namespace SharpView
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Logger.Write_Output($@"Ex: SharpView.exe Method-Name -Switch -String domain -Array domain,user -Enum ResetPassword -IntEnum CREATED_BY_SYSTEM,APP_BASIC -PointEnum ResetPassword,All -Credential admin@domain.local/password");
                Logger.Write_Output($@"Execute 'Sharpview.exe <Method-Name> -Help' to get arguments list and expected types");
                return;
            }
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                Logger.Write_Warning($@"An errror occured : {e.Message}");

            }
        }

        static void Run(string[] args)
        {
            var methodName = args[0];
            methodName = methodName.ToLower().Replace("-", "_");

            var method = typeof(PowerView).GetMethod(methodName, 
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.IgnoreCase);
            if (method == null)
            {
                Logger.Write_Warning($@"Invalid method '{methodName}'");
                return;
            }
            if(args.Length > 1 && (args[1].ToLower() == "-help" || args[1].ToLower() == "help"))
            {
                Logger.Write_Output(Environment.NewLine + GetMethodHelp(method));
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters == null || parameters.Length != 1)
            {
                Logger.Write_Warning($@"Method has no parameters");
                return;
            }
            Type paramType = Type.GetType(parameters[0].ParameterType.FullName);
            if (paramType == null)
            {
                Logger.Write_Warning($@"No type for '{parameters[0].ParameterType.FullName}'");
                return;
            }

            object argObject = Activator.CreateInstance(paramType, false);
            if (argObject != null)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    var argName = args[i];
                    if (argName.StartsWith("-"))
                    {
                        argName = argName.TrimStart(new[] { '-' });
                        //PropertyInfo pinfo = paramType.GetProperty(argName);
                        PropertyInfo pinfo = parameters[0].ParameterType
                            .GetProperties(BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance)
                            .FirstOrDefault(p => String.Equals(p.Name, argName, StringComparison.OrdinalIgnoreCase));
                        if (pinfo == null)
                            continue;
                        i++;
                        try
                        {
                            var strValue = "";
                            if (i < args.Length)
                                strValue = args[i];
                            else
                            {
                                if (pinfo.PropertyType.FullName == "System.Boolean")
                                    strValue = "true";
                            }

                            if (pinfo.PropertyType.FullName == "System.Security.SecureString")
                            {
                                pinfo.SetValue(argObject, ConvertToSecureString(strValue));
                                continue;
                            }

                            TypeConverter tc = TypeDescriptor.GetConverter(pinfo.PropertyType);
                            if (tc is BooleanConverter)
                            {
                                if (strValue.StartsWith("-"))
                                {
                                    i--;
                                    strValue = "true";
                                }
                            }
                            else if (tc is ArrayConverter)
                                tc = new StringArrayConverter();
                            else if (pinfo.PropertyType.FullName == "System.Net.NetworkCredential")
                                tc = new NetworkCredentialConverter();
                            var argValue = tc.ConvertFromString(strValue);
                            pinfo.SetValue(argObject, argValue);
                        }
                        catch (Exception ex)
                        {
                            Logger.Write_Warning($@"Parsing Error {argName}: {ex.Message}");
                        }
                    }
                }
            }
            // Leaving out try catch block to see errors for now

            var ret = method.Invoke(null, new[] { argObject });
            ObjectDumper.Write(ret);
        }

        static SecureString ConvertToSecureString(string password)
        {
            if (password == null)
                throw new ArgumentNullException("password");

            var securePassword = new SecureString();

            foreach (char c in password)
                securePassword.AppendChar(c);

            securePassword.MakeReadOnly();
            return securePassword;
        }

        static string GetMethodHelp(MethodInfo method)
        {
            var helpArgs = "";
            var args = method.GetParameters();
            foreach (var arg in args)
            {
                helpArgs += GetClassHelpAsParameter(arg);
            }
            return $@"{method.Name} {helpArgs}";
        }

        static string GetClassHelpAsParameter(ParameterInfo parameter)
        {
            var type = Type.GetType(parameter.ParameterType.FullName);
            var info = type.GetTypeInfo();

            IEnumerable<PropertyInfo> pList = info.DeclaredProperties;

            StringBuilder sb = new StringBuilder();

            foreach (PropertyInfo p in pList)
            {
                sb.Append($@"-{p.Name} <{p.PropertyType.Name}> ");
            }
            return sb.ToString();
        }
    }
}
