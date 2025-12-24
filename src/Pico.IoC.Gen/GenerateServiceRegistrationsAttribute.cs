namespace Pico.IoC.Gen;

/// <summary>
/// Marks a class that uses Pico.IoC registration methods to have its service descriptors generated at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class GenerateServiceRegistrationsAttribute : Attribute;
