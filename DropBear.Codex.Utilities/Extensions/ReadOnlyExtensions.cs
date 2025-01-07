#region

using System.Reflection;
using System.Reflection.Emit;

#endregion

namespace DropBear.Codex.Utilities.Extensions;

/// <summary>
///     Provides an extension method to create a read-only version of an object by dynamically generating a wrapper type.
/// </summary>
public static class ReadOnlyExtensions
{
    private static readonly Dictionary<Type, Type> CachedReadOnlyTypes = new();

    /// <summary>
    ///     Returns a read-only version of the specified object.
    ///     If the object is a value type, the original object is returned.
    ///     If the object is a reference type, a dynamically generated read-only wrapper is returned.
    /// </summary>
    /// <param name="obj">The object to create a read-only version of.</param>
    /// <returns>A read-only version of the object, or the original object if it is a value type.</returns>
    public static object? GetReadOnlyVersion(this object? obj)
    {
        if (obj is null)
        {
            return null;
        }

        var type = obj.GetType();

        // For value types (structs), just return the original instance
        if (type.IsValueType)
        {
            return obj;
        }

        // For reference types (classes), create or retrieve a read-only wrapper
        var readOnlyType = GetOrCreateReadOnlyType(type);
        return Activator.CreateInstance(readOnlyType, obj);
    }

    /// <summary>
    ///     Retrieves a cached read-only type if it exists, or creates a new one if it doesn't.
    /// </summary>
    /// <param name="type">The original type to create a read-only version of.</param>
    /// <returns>A dynamically generated read-only type.</returns>
    private static Type GetOrCreateReadOnlyType(Type type)
    {
        if (CachedReadOnlyTypes.TryGetValue(type, out var readOnlyType))
        {
            return readOnlyType;
        }

        readOnlyType = CreateReadOnlyType(type);
        CachedReadOnlyTypes[type] = readOnlyType;
        return readOnlyType;
    }

    /// <summary>
    ///     Dynamically generates a read-only version of the specified type.
    /// </summary>
    /// <param name="type">The original type to create a read-only version of.</param>
    /// <returns>A dynamically generated read-only type.</returns>
    private static Type CreateReadOnlyType(Type type)
    {
        var assemblyName = new AssemblyName("ReadOnlyAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("ReadOnlyModule");
        var typeName = $"ReadOnly{type.Name}";
        var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);

        typeBuilder.DefineDefaultConstructor(MethodAttributes.Public);

        var instanceField =
            typeBuilder.DefineField("_instance", type, FieldAttributes.Private | FieldAttributes.InitOnly);

        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.HasThis,
            [type]
        );

        var constructorIl = constructor.GetILGenerator();
        constructorIl.Emit(OpCodes.Ldarg_0);
        constructorIl.Emit(OpCodes.Ldarg_1);
        constructorIl.Emit(OpCodes.Stfld, instanceField);
        constructorIl.Emit(OpCodes.Ret);

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var getMethod = property.GetGetMethod();
            if (getMethod is null)
            {
                continue; // Skip properties without a getter
            }

            var readOnlyProperty =
                typeBuilder.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, null);
            var getMethodBuilder = typeBuilder.DefineMethod($"get_{property.Name}",
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig, property.PropertyType,
                Type.EmptyTypes);

            var getIl = getMethodBuilder.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, instanceField);
            getIl.Emit(OpCodes.Call, getMethod);
            getIl.Emit(OpCodes.Ret);

            readOnlyProperty.SetGetMethod(getMethodBuilder);
        }

        return typeBuilder.CreateType();
    }
}
