using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;

namespace Veldrath.Discord.Tests.Infrastructure;

/// <summary>
/// Executes real slash-command module bodies in-process and records what they send back to Discord.
/// </summary>
/// <remarks>
/// <para>
/// Discord.Net gives a module its context through <see cref="InteractionModuleBase{T}.Context"/>, whose setter is
/// private, and the context's <c>Interaction</c> is the abstract <see cref="SocketInteraction"/> — whose only
/// constructors are internal to <c>Discord.Net.WebSocket</c>. There is therefore no public seam a test can use to
/// hand a module a fake interaction, so this harness builds one:
/// </para>
/// <list type="number">
/// <item>
/// It emits a tiny <see cref="SocketInteraction"/> subclass at runtime (a dynamic assembly granted access to
/// <c>Discord.Net.WebSocket</c> internals through <see cref="IgnoresAccessChecksToAttribute"/> — the runtime-level
/// escape hatch for exactly this case). Every abstract member of <see cref="SocketInteraction"/> is implemented by
/// forwarding to an <see cref="InteractionTranscript"/>, which is how the calls a module makes are captured.
/// </item>
/// <item>
/// It builds a real <see cref="SocketInteractionContext"/> around that interaction and assigns it to the module
/// through the private <c>Context</c> setter.
/// </item>
/// </list>
/// <para>
/// This is a test-only seam: it executes the product's real command bodies (validation, repository lookups, embed
/// building, and the Discord calls they issue) — nothing is stubbed except the Discord transport itself. Like every
/// other path in this test project, the harness is excluded from coverage by
/// <c>[assembly: ExcludeFromCodeCoverage]</c>; its purpose is to make production code reachable, not to stand in for
/// it.
/// </para>
/// </remarks>
public sealed class DiscordInteractionHarness
{
    /// <summary>Gets the calls the module issued, in order, with the payloads it produced.</summary>
    public InteractionTranscript Transcript { get; }

    /// <summary>Gets the context handed to the module.</summary>
    public SocketInteractionContext Context { get; }

    private static readonly Lazy<Type> RecordingInteractionType = new(CreateRecordingInteractionType);

    // A single client instance is enough: the modules never touch the gateway, only Context.Interaction.
    private static readonly Lazy<DiscordSocketClient> Client = new(() => new DiscordSocketClient());

    private static readonly PropertyInfo ContextProperty =
        typeof(InteractionModuleBase<SocketInteractionContext>).GetProperty(nameof(InteractionModuleBase<SocketInteractionContext>.Context))!;

    private DiscordInteractionHarness(InteractionTranscript transcript, SocketInteractionContext context)
    {
        Transcript = transcript;
        Context = context;
    }

    /// <summary>Creates a harness with a fresh transcript.</summary>
    /// <returns>The harness, ready to <see cref="Attach{TModule}"/> a module.</returns>
    public static DiscordInteractionHarness Create()
    {
        var transcript = new InteractionTranscript();
        var interaction = (SocketInteraction)Activator.CreateInstance(
            RecordingInteractionType.Value, Client.Value, null, null, transcript)!;

        return new DiscordInteractionHarness(transcript, new SocketInteractionContext(Client.Value, interaction));
    }

    /// <summary>Binds a module instance to this harness's context so its slash-command bodies can run.</summary>
    /// <typeparam name="TModule">Concrete module type being tested.</typeparam>
    /// <param name="module">The module instance to bind.</param>
    /// <returns>The same instance, for fluent use.</returns>
    public TModule Attach<TModule>(TModule module)
        where TModule : InteractionModuleBase<SocketInteractionContext>
    {
        ContextProperty.SetValue(module, Context);
        return module;
    }

    private static Type CreateRecordingInteractionType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Veldrath.Discord.Tests.DynamicInteraction"),
            AssemblyBuilderAccess.Run);

        // Lets the emitted type call SocketInteraction's internal constructor and override its abstract members.
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(IgnoresAccessChecksToAttribute).GetConstructor([typeof(string)])!,
            ["Discord.Net.WebSocket"]));

        var moduleBuilder = assembly.DefineDynamicModule("Veldrath.Discord.Tests.DynamicInteraction");
        var baseType = typeof(SocketInteraction);
        var typeBuilder = moduleBuilder.DefineType(
            "Veldrath.Discord.Tests.DynamicInteraction.RecordingInteraction",
            TypeAttributes.Public | TypeAttributes.Class,
            baseType);

        var transcriptField = typeBuilder.DefineField("Transcript", typeof(InteractionTranscript), FieldAttributes.Public);
        var respondedField = typeBuilder.DefineField("_hasResponded", typeof(bool), FieldAttributes.Private);

        EmitConstructor(typeBuilder, baseType, transcriptField);
        EmitOverrides(typeBuilder, baseType, transcriptField, respondedField);

        return typeBuilder.CreateType()!;
    }

    private static void EmitConstructor(TypeBuilder typeBuilder, Type baseType, FieldInfo transcriptField)
    {
        // SocketInteraction(DiscordSocketClient client, ulong id, ISocketMessageChannel channel, SocketUser user)
        var baseConstructor = baseType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();

        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            [typeof(DiscordSocketClient), typeof(ISocketMessageChannel), typeof(SocketUser), typeof(InteractionTranscript)]);

        var il = constructor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Conv_I8);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Ldarg_3);
        il.Emit(OpCodes.Call, baseConstructor);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg, (short)4);
        il.Emit(OpCodes.Stfld, transcriptField);
        il.Emit(OpCodes.Ret);
    }

    private static void EmitOverrides(
        TypeBuilder typeBuilder,
        Type baseType,
        FieldInfo transcriptField,
        FieldInfo respondedField)
    {
        foreach (var abstractMethod in baseType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.IsAbstract && method.DeclaringType == baseType))
        {
            var parameters = abstractMethod.GetParameters();
            var method = typeBuilder.DefineMethod(
                abstractMethod.Name,
                MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                abstractMethod.ReturnType,
                parameters.Select(parameter => parameter.ParameterType).ToArray());

            var il = method.GetILGenerator();
            if (abstractMethod.Name is "get_HasResponded" or "set_HasResponded")
            {
                il.Emit(OpCodes.Ldarg_0);
                if (abstractMethod.Name == "get_HasResponded")
                {
                    il.Emit(OpCodes.Ldfld, respondedField);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Stfld, respondedField);
                }

                il.Emit(OpCodes.Ret);
            }
            else
            {
                // Every Discord call forwards (op name + boxed arguments) to the transcript, then returns the task it
                // got back. Followups return Task<RestFollowupMessage>, everything else returns Task.
                var dispatcher = typeof(InteractionTranscript)
                    .GetMethod(abstractMethod.ReturnType.IsGenericType
                        ? nameof(InteractionTranscript.RecordFollowup)
                        : nameof(InteractionTranscript.RecordVoid))!;

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, transcriptField);
                il.Emit(OpCodes.Ldstr, abstractMethod.Name);
                il.Emit(OpCodes.Ldc_I4, parameters.Length);
                il.Emit(OpCodes.Newarr, typeof(object));
                for (var i = 0; i < parameters.Length; i++)
                {
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldarg, (short)(i + 1));
                    if (parameters[i].ParameterType.IsValueType)
                        il.Emit(OpCodes.Box, parameters[i].ParameterType);
                    il.Emit(OpCodes.Stelem_Ref);
                }

                il.Emit(OpCodes.Callvirt, dispatcher);
                il.Emit(OpCodes.Ret);
            }

            typeBuilder.DefineMethodOverride(method, abstractMethod);
        }
    }
}

/// <summary>
/// Records every Discord call a module makes and exposes the payloads worth asserting on.
/// </summary>
public sealed class InteractionTranscript
{
    private static readonly Dictionary<string, string[]> ParameterNamesByOperation = typeof(SocketInteraction)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(method => method.IsAbstract && method.DeclaringType == typeof(SocketInteraction))
        .GroupBy(method => method.Name)
        .ToDictionary(
            group => group.Key,
            group => group.First().GetParameters().Select(parameter => parameter.Name!).ToArray());

    private readonly List<string> _operations = [];

    /// <summary>Gets the names of the calls made, in order (e.g. <c>DeferAsync</c>, then <c>FollowupAsync</c>).</summary>
    public IReadOnlyList<string> Operations => _operations;

    /// <summary>Gets a value indicating whether the module deferred its reply.</summary>
    public bool Deferred { get; private set; }

    /// <summary>Gets the ephemeral flag of the last message sent, when one was supplied.</summary>
    public bool? Ephemeral { get; private set; }

    /// <summary>Gets the ephemeral flag the module deferred with, when it deferred.</summary>
    public bool? DeferEphemeral { get; private set; }

    /// <summary>Gets the plain-text body of the last message sent, when it was a text reply.</summary>
    public string? Text { get; private set; }

    /// <summary>Gets the embed of the last message sent, when it was an embed reply.</summary>
    public Embed? Embed { get; private set; }

    /// <summary>Gets the embeds of the last message sent, when the call carried an embed array.</summary>
    public IReadOnlyList<Embed> Embeds { get; private set; } = [];

    /// <summary>Gets or sets a value indicating whether the interaction reported itself as already answered.</summary>
    public bool HasResponded { get; set; }

    /// <summary>Records a call whose Discord API returns a plain task.</summary>
    /// <param name="operation">Discord API member name.</param>
    /// <param name="arguments">Arguments the module passed, boxed.</param>
    /// <returns>A completed task.</returns>
    public Task RecordVoid(string operation, object?[] arguments)
    {
        Record(operation, arguments);
        return Task.CompletedTask;
    }

    /// <summary>Records a call whose Discord API returns the created follow-up message.</summary>
    /// <param name="operation">Discord API member name.</param>
    /// <param name="arguments">Arguments the module passed, boxed.</param>
    /// <returns>A completed task with no message body, as no REST call happened.</returns>
    public Task<RestFollowupMessage> RecordFollowup(string operation, object?[] arguments)
    {
        Record(operation, arguments);
        return Task.FromResult<RestFollowupMessage>(null!);
    }

    private void Record(string operation, object?[] arguments)
    {
        _operations.Add(operation);

        if (operation == nameof(SocketInteraction.DeferAsync))
            Deferred = true;

        var names = ParameterNamesByOperation[operation];
        for (var i = 0; i < names.Length; i++)
        {
            switch (names[i])
            {
                case "text":
                    Text = arguments[i] as string;
                    break;
                case "embed":
                    if (arguments[i] is Embed embed)
                        Embed = embed;
                    break;
                case "embeds":
                    if (arguments[i] is Embed[] embeds)
                        Embeds = embeds;
                    break;
                case "ephemeral":
                    if (arguments[i] is bool ephemeral)
                    {
                        Ephemeral = ephemeral;
                        if (operation == nameof(SocketInteraction.DeferAsync))
                            DeferEphemeral = ephemeral;
                    }

                    break;
            }
        }
    }
}
