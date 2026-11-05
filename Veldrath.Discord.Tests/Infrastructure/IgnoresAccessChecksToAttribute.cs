using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Runtime-recognised marker that lets a dynamic assembly call another assembly's non-public members.
/// </summary>
/// <remarks>
/// This is a re-declaration of the attribute the CoreCLR looks for by name; it exists so
/// <see cref="Veldrath.Discord.Tests.Infrastructure.DiscordInteractionHarness"/> can emit a
/// <c>SocketInteraction</c> subclass even though Discord.Net keeps that type's constructor internal.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class IgnoresAccessChecksToAttribute(string assemblyName) : Attribute
{
    /// <summary>Gets the name of the assembly whose internals may be accessed.</summary>
    public string AssemblyName { get; } = assemblyName;
}
