using System.Reflection;

using Type = System.Type;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Protoc.Gateway;

public static class XmlDocsGetter
{
    private static readonly HashSet<Assembly> s_loadedAssemblies = new();
    private static readonly Dictionary<string, string> s_loadedXmlDocumentation = new();

    public static string GetMethodComment(this MethodInfo methodInfo)
    {
        string str1 = string.Join(',', methodInfo.GetParameters().Select(p => p.ParameterType.FullName));
        Dictionary<string, string> xmlDocumentation = s_loadedXmlDocumentation;
        DefaultInterpolatedStringHandler interpolatedStringHandler = new(5, 3);
        interpolatedStringHandler.AppendLiteral("M:");
        interpolatedStringHandler.AppendFormatted(methodInfo.DeclaringType?.FullName);
        interpolatedStringHandler.AppendLiteral(".");
        interpolatedStringHandler.AppendFormatted(methodInfo.Name);
        interpolatedStringHandler.AppendLiteral("(");
        interpolatedStringHandler.AppendFormatted(str1);
        interpolatedStringHandler.AppendLiteral(")");
        string key = interpolatedStringHandler.ToStringAndClear().Replace('+', '.');
        return xmlDocumentation.TryGetValue(key, out string? str2) ? str2.TrimValue() : string.Empty;
    }

    public static string GetTypeComment(this Type type)
    {
        return s_loadedXmlDocumentation.TryGetValue("T:" + type.FullName, out string? str) ? str.TrimValue() : string.Empty;
    }

    public static string GetClientComment(this Type type)
    {
        return s_loadedXmlDocumentation.TryGetValue("T:" + type.DeclaringType?.FullName, out string? str) ? str.TrimValue() : string.Empty;
    }

    public static string GetPropertyComment(this PropertyInfo propertyInfo)
    {
        string key = propertyInfo.DeclaringType?.FullName + "." + propertyInfo.Name;
        return s_loadedXmlDocumentation.TryGetValue("P:" + key, out string? str) ? str.TrimValue() : string.Empty;
    }

    public static string GetDocumentation(this Type type)
    {
        LoadXmlDocumentation(type.Assembly);
        return string.Empty;
    }

    private static string TrimValue(this string value) => ((value.Split("<summary>", StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? string.Empty).Split("</summary>", StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty).Trim('\n').Trim('\t').Trim();

    private static void LoadXmlDocumentation(Assembly assembly)
    {
        if (s_loadedAssemblies.Contains(assembly))
        {
            return;
        }

        string path = Path.Combine(AppContext.BaseDirectory, assembly.GetName().Name + ".xml");
        if (!File.Exists(path))
        {
            return;
        }

        LoadXmlDocumentation(File.ReadAllText(path));
        s_loadedAssemblies.Add(assembly);
    }

    public static void LoadXmlDocumentation(string xmlDocumentation)
    {
        using XmlReader xmlReader = XmlReader.Create(new StringReader(xmlDocumentation));

        while (xmlReader.Read())
        {
            if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
            {
                string? key = xmlReader["name"];
                if (key is not null)
                {
                    s_loadedXmlDocumentation[key] = xmlReader.ReadInnerXml();
                }
            }
        }
    }
}