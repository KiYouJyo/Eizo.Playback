using System.Reflection;
using Eizo.Playback.Backends.LibVLC;

namespace Eizo.Playback.LibVLC.Tests;

public sealed class PublicApiIsolationTests
{
    [Fact]
    public void AbstractionsAssemblyDoesNotReferenceBackendOrUiFrameworks()
    {
        var references = typeof(IPlaybackEngine)
            .Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .Where(static name => name is not null)
            .ToArray();

        Assert.DoesNotContain(
            references,
            static name => name!.StartsWith("LibVLCSharp", StringComparison.Ordinal));

        Assert.DoesNotContain(
            references,
            static name => name!.StartsWith("Microsoft.UI.Xaml", StringComparison.Ordinal));

        Assert.DoesNotContain(
            references,
            static name => name!.StartsWith("Microsoft.WindowsAppSDK", StringComparison.Ordinal));
    }

    [Fact]
    public void LibVlcBackendPublicSurfaceDoesNotExposeLibVlcSharpTypes()
    {
        var assembly = typeof(LibVlcPlaybackEngine).Assembly;

        var leaks = assembly
            .GetExportedTypes()
            .SelectMany(GetPublicSurfaceTypes)
            .Where(static type =>
                type.Namespace?.StartsWith(
                    "LibVLCSharp",
                    StringComparison.Ordinal) == true)
            .Select(static type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaks);
    }

    [Fact]
    public void AbstractionsPublicSurfaceRemainsBackendNeutral()
    {
        var assembly = typeof(IPlaybackEngine).Assembly;

        var leaks = assembly
            .GetExportedTypes()
            .SelectMany(GetPublicSurfaceTypes)
            .Where(static type =>
                type.Namespace?.StartsWith(
                    "LibVLCSharp",
                    StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith(
                    "Microsoft.UI.Xaml",
                    StringComparison.Ordinal) == true)
            .Select(static type => type.FullName ?? type.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(leaks);
    }

    private static IEnumerable<Type> GetPublicSurfaceTypes(Type exportedType)
    {
        yield return exportedType;

        if (exportedType.BaseType is not null)
        {
            foreach (var type in FlattenType(exportedType.BaseType))
            {
                yield return type;
            }
        }

        foreach (var interfaceType in exportedType.GetInterfaces())
        {
            foreach (var type in FlattenType(interfaceType))
            {
                yield return type;
            }
        }

        const BindingFlags flags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.DeclaredOnly;

        foreach (var property in exportedType.GetProperties(flags))
        {
            foreach (var type in FlattenType(property.PropertyType))
            {
                yield return type;
            }
        }

        foreach (var field in exportedType.GetFields(flags))
        {
            foreach (var type in FlattenType(field.FieldType))
            {
                yield return type;
            }
        }

        foreach (var eventInfo in exportedType.GetEvents(flags))
        {
            if (eventInfo.EventHandlerType is null)
            {
                continue;
            }

            foreach (var type in FlattenType(eventInfo.EventHandlerType))
            {
                yield return type;
            }
        }

        foreach (var constructor in exportedType.GetConstructors(flags))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                foreach (var type in FlattenType(parameter.ParameterType))
                {
                    yield return type;
                }
            }
        }

        foreach (var method in exportedType.GetMethods(flags))
        {
            if (method.IsSpecialName)
            {
                continue;
            }

            foreach (var type in FlattenType(method.ReturnType))
            {
                yield return type;
            }

            foreach (var parameter in method.GetParameters())
            {
                foreach (var type in FlattenType(parameter.ParameterType))
                {
                    yield return type;
                }
            }
        }
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            var elementType = type.GetElementType();

            if (elementType is not null)
            {
                foreach (var flattened in FlattenType(elementType))
                {
                    yield return flattened;
                }
            }

            yield break;
        }

        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var flattened in FlattenType(argument))
            {
                yield return flattened;
            }
        }
    }
}
