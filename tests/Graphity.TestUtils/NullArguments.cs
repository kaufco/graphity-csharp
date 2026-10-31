using System.Reflection;

namespace Graphity.TestUtils;

/// <summary>
/// Checks the null contract of an assembly: every public method and constructor of its public types
/// throws <see cref="ArgumentNullException"/>, naming the parameter, for a <see langword="null"/>
/// argument where the type of the parameter does not allow one.
/// </summary>
/// <remarks>
/// The members are found by reflection, so a member added later is checked without a change to the
/// tests. A member that takes or returns a span, and a generic one, cannot be called by reflection;
/// <see cref="Check"/> returns those that also take a parameter to check, and the caller tests them by
/// hand.
/// </remarks>
public static class NullArguments
{
    /// <summary>The outcome of <see cref="Check"/>.</summary>
    /// <param name="Failures">One line per parameter that does not throw as required.</param>
    /// <param name="Checked">The number of parameters checked.</param>
    /// <param name="NotCallable">
    /// The members that reflection cannot call, as <c>Type.Member(Parameters)</c>.
    /// </param>
    public sealed record Result(IReadOnlyList<string> Failures, int Checked, IReadOnlyList<string> NotCallable);

    /// <summary>
    /// Calls every public member of the public types of <paramref name="assembly"/> once per
    /// reference-type parameter outside the nullable contract, with <see langword="null"/> for it and
    /// <paramref name="sample"/> values for the instance and the other reference-type parameters;
    /// value-type parameters get their default.
    /// </summary>
    /// <param name="assembly">The assembly to check.</param>
    /// <param name="sample">Returns a non-null value of a type, or of a type derived from it.</param>
    /// <returns>What failed, how much was checked, and what the caller tests by hand.</returns>
    public static Result Check(Assembly assembly, Func<Type, object> sample)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(sample);
        var nullability = new NullabilityInfoContext();
        var failures = new List<string>();
        var notCallable = new List<string>();
        int checkedCount = 0;
        foreach (Type type in assembly.GetExportedTypes())
        {
            if (type.IsSubclassOf(typeof(Delegate)))
            {
                continue;
            }
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance
                | BindingFlags.DeclaredOnly;
            IEnumerable<MethodBase> members = type.GetMethods(Flags).Cast<MethodBase>()
                .Concat(type.IsAbstract ? [] : type.GetConstructors(Flags));
            foreach (MethodBase member in members)
            {
                ParameterInfo[] parameters = member.GetParameters();
                int[] toCheck = [.. Enumerable.Range(0, parameters.Length)
                    .Where(i => MustNotBeNull(parameters[i], nullability))];
                if (toCheck.Length == 0)
                {
                    continue;
                }
                string name = $"{type.Name}.{member.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name))})";
                if (member.ContainsGenericParameters || parameters.Any(p => p.ParameterType.IsByRefLike)
                    || (member is MethodInfo method && method.ReturnType.IsByRefLike))
                {
                    notCallable.Add(name);
                    continue;
                }
                foreach (int i in toCheck)
                {
                    checkedCount++;
                    object?[] args = [.. parameters.Select(p => ArgumentFor(p.ParameterType, sample))];
                    args[i] = null;
                    string? failure = CallWithNull(member, type, args, parameters[i].Name, sample);
                    if (failure is not null)
                    {
                        failures.Add($"{name} with null for {parameters[i].Name}: {failure}");
                    }
                }
            }
        }
        return new Result(failures, checkedCount, notCallable);
    }

    private static bool MustNotBeNull(ParameterInfo parameter, NullabilityInfoContext nullability)
    {
        Type type = parameter.ParameterType;
        if (type.IsValueType || type.IsByRef || type.IsPointer || type.IsGenericParameter)
        {
            return false;
        }
        return nullability.Create(parameter).WriteState == NullabilityState.NotNull;
    }

    private static object? ArgumentFor(Type type, Func<Type, object> sample) =>
        type.IsValueType ? Activator.CreateInstance(type) : sample(type);

    // Returns null if the call throws ArgumentNullException for the parameter, else what it did.
    private static string? CallWithNull(MethodBase member, Type type, object?[] args, string? parameterName,
        Func<Type, object> sample)
    {
        try
        {
            if (member is ConstructorInfo constructor)
            {
                constructor.Invoke(args);
            }
            else
            {
                member.Invoke(member.IsStatic ? null : sample(type), args);
            }
            return "returned normally";
        }
        catch (TargetInvocationException e) when (e.InnerException is ArgumentNullException inner)
        {
            return inner.ParamName == parameterName
                ? null
                : $"ArgumentNullException for {inner.ParamName ?? "no parameter"}";
        }
        catch (TargetInvocationException e)
        {
            return e.InnerException?.GetType().Name ?? e.GetType().Name;
        }
    }
}
