#region

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Reflection.Emit;
using DropBear.Codex.Core.Results.Base;
using DropBear.Codex.Utilities.Errors;

#endregion

namespace DropBear.Codex.Utilities.Extensions;

/// <summary>
///     Provides an extension method to create a read-only version of an object by dynamically generating a wrapper type.
///     Optimized for .NET 8 performance features.
/// </summary>
public static class ReadOnlyExtensions
{
    private static readonly FrozenDictionary<Type, Type> CachedReadOnlyTypes =
        new ConcurrentDictionary<Type, Type>().ToFrozenDictionary();


    /// <summary>
    ///     Returns a read-only version of the specified object.
    ///     If the object is a value type, the original object is returned.
    ///     If the object is a reference type, a dynamically generated read-only wrapper is returned.
    /// </summary>
    /// <param name="obj">The object to create a read-only version of.</param>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <returns>
    ///     A <see cref="Result{T, ReadOnlyConversionError}" /> containing the read-only version of the object or an
    ///     error.
    /// </returns>
    public static Result<T, ReadOnlyConversionError> GetReadOnlyVersion<T>(this T obj)
    {
        if (obj is null)
        {
            return Result<T, ReadOnlyConversionError>.Failure(new ReadOnlyConversionError("Object cannot be null."));
        }

        var type = typeof(T);

        // If the object is a value type, return as-is
        if (type.IsValueType)
        {
            return Result<T, ReadOnlyConversionError>.Success(obj);
        }

        try
        {
            var readOnlyType = GetOrCreateReadOnlyType(type);
            var readOnlyInstance = Activator.CreateInstance(readOnlyType, obj);
            return Result<T, ReadOnlyConversionError>.Success((T)readOnlyInstance!);
        }
        catch (Exception ex)
        {
            return Result<T, ReadOnlyConversionError>.Failure(
                new ReadOnlyConversionError("Failed to create read-only version.", ex));
        }
    }

    /// <summary>
    ///     Retrieves a cached read-only type if it exists, or creates a new one if it doesn't.
    /// </summary>
    /// <param name="type">The original type to create a read-only version of.</param>
    /// <returns>A dynamically generated read-only type.</returns>
    private static Type GetOrCreateReadOnlyType(Type type)
    {
        return CachedReadOnlyTypes.GetValueOrDefault(type) ?? CreateReadOnlyType(type);
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

        var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, [type]);
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

        return typeBuilder.CreateType()!;
    }
}
