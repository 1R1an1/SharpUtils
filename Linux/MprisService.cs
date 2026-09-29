/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Tmds.DBus;

namespace SharpUtils.Linux;

/// <summary>
/// Fuente de estado y acciones que <see cref="MprisService"/> expone por D-Bus.
/// Es abstracta: se usa heredando y asignando los campos desde la subclase.
/// Los métodos son virtuales no-op; overrideá solo los que tu app soporte.
/// </summary>
public abstract class MprisSource
{
    public string Title = "";
    public string Artist = "";
    public bool IsPlaying = false;

    /// <summary>Duración del track actual, en microsegundos.</summary>
    public long DurationUs = 0;

    /// <summary>Posición actual del track, en microsegundos.</summary>
    public long PositionUs = 0;

    public string CoverHashHex = "";
    public byte[] CoverBytes = null;

    protected internal virtual void TogglePlayPause() { }

    /// <param name="offsetUs">Offset relativo en microsegundos (positivo o negativo).</param>
    protected internal virtual void Seek(long offsetUs) { }

    /// <param name="positionUs">Posición absoluta en microsegundos.</param>
    protected internal virtual void SetPosition(long positionUs) { }

    protected internal virtual void Stop() { }
    protected internal virtual void Next() { }
    protected internal virtual void Prev() { }
    protected internal virtual void Quit() { }
    protected internal virtual void Raise() { }
    protected internal virtual void OpenUri(string uri) { }

    /// <summary>"None", "Track" o "Playlist". Asignar solo si se declara <see cref="MprisCapabilities.SupportsLoop"/>.</summary>
    public string LoopStatus = "None";

    /// <summary>Asignar solo si se declara <see cref="MprisCapabilities.SupportsShuffle"/>.</summary>
    public bool Shuffle = false;

    /// <summary>Rango 0.0 a 1.0. Asignar solo si se declara <see cref="MprisCapabilities.SupportsVolume"/>.</summary>
    public double Volume = 1.0;

    /// <summary>Velocidad de reproducción. Asignar solo si se declara <see cref="MprisCapabilities.SupportsRate"/>.</summary>
    public double Rate = 1.0;

    /// <summary>Invocado por MPRIS cuando el escritorio cambia <see cref="LoopStatus"/>. Overrideá para reaccionar.</summary>
    /// <param name="loop">Nuevo valor de <see cref="LoopStatus"/> ("None", "Track" o "Playlist").</param>
    protected internal virtual void LoopChanged(string loop) { }

    /// <summary>Invocado por MPRIS cuando el escritorio cambia <see cref="Shuffle"/>. Overrideá para reaccionar.</summary>
    /// <param name="shuffle">Nuevo valor de <see cref="Shuffle"/>.</param>
    protected internal virtual void ShuffleChanged(bool shuffle) { }

    /// <summary>Invocado por MPRIS cuando el escritorio cambia <see cref="Volume"/>. Overrideá para reaccionar.</summary>
    /// <param name="volume">Nuevo valor de <see cref="Volume"/> (0.0 a 1.0).</param>
    protected internal virtual void VolumeChanged(double volume) { }

    /// <summary>Invocado por MPRIS cuando el escritorio cambia <see cref="Rate"/>. Overrideá para reaccionar.</summary>
    /// <param name="rate">Nuevo valor de <see cref="Rate"/>.</param>
    protected internal virtual void RateChanged(double rate) { }
}

/// <summary>
/// Declara qué funcionalidades soporta tu app.
/// Los <c>CanX</c> controlan qué botones muestra el escritorio en el widget MPRIS;
/// los <c>SupportsX</c> habilitan la lectura/escritura de propiedades asociadas.
/// </summary>
public sealed record class MprisCapabilities
{
    /// <summary>Si la app puede cerrarse vía <see cref="MprisSource.Quit"/>.</summary>
    public bool CanQuit { get; set; } = false;

    /// <summary>Si la app puede traer su ventana al frente vía <see cref="MprisSource.Raise"/>.</summary>
    public bool CanRaise { get; init; } = false;

    /// <summary>Si la app puede iniciar reproducción.</summary>
    public bool CanPlay { get; set; } = true;

    /// <summary>Si la app puede pausar.</summary>
    public bool CanPause { get; set; } = true;

    /// <summary>Si la app permite cambiar la posición del track.</summary>
    public bool CanSeek { get; set; } = true;

    /// <summary>Si la app puede ir al track siguiente.</summary>
    public bool CanGoNext { get; set; } = true;

    /// <summary>Si la app puede ir al track anterior.</summary>
    public bool CanGoPrevious { get; set; } = true;

    /// <summary>Si la app puede detener (distinto de pausar: resetea la posición).</summary>
    public bool CanStop { get; set; } = false;

    /// <summary>Si el escritorio puede mandar com1andos en absoluto. Casi siempre true.</summary>
    public bool CanControl { get; set; } = true;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.LoopStatus"/>.</summary>
    public bool SupportsLoop { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Shuffle"/>.</summary>
    public bool SupportsShuffle { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Volume"/>.</summary>
    public bool SupportsVolume { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Rate"/>.</summary>
    public bool SupportsRate { get; init; } = false;

    /// <summary>Esquemas de URI que la app acepta en <see cref="MprisSource.OpenUri"/> (p.ej. "file", "http").</summary>
    public string[] SupportedUriSchemes { get; init; } = Array.Empty<string>();

    /// <summary>Tipos MIME que la app puede abrir (p.ej. "audio/mpeg", "audio/ogg").</summary>
    public string[] SupportedMimeTypes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Configuración por app. Solo dos campos; el resto (bus name, desktop entry,
/// cache dir, trackid prefix) se deriva automáticamente.
/// </summary>
public sealed record class MprisOptions
{
    private string _name;
    /// <summary>Nombre interno de la app, sin espacios y en minúsculas (p.ej. "sharputils").</summary>
    public required string Name { get { return _name; } init { _name = value.Replace(" ", "").ToLowerInvariant(); } }

    /// <summary>Nombre visible para el usuario en el widget MPRIS. Si queda vacío, se usa <see cref="Name"/>.</summary>
    public string DisplayName { get; init; } = "";
}

/// <summary>Payload de la señal PropertiesChanged de D-Bus.</summary>
public struct PropertyChanges
{
    public IDictionary<string, object> Changed;
    public string[] Invalidated;
}

[DBusInterface("org.mpris.MediaPlayer2",
    GetPropertyMethod = "GetAsync", SetPropertyMethod = "SetAsync",
    GetAllPropertiesMethod = "GetAllAsync", WatchPropertiesMethod = "WatchPropertiesAsync")]
public interface IMprisRoot : IDBusObject
{
    Task RaiseAsync();
    Task QuitAsync();

    Task<object> GetAsync(string property);
    Task SetAsync(string property, object value);
    Task<IDictionary<string, object>> GetAllAsync();
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
}

[DBusInterface("org.mpris.MediaPlayer2.Player",
    GetPropertyMethod = "GetAsync", SetPropertyMethod = "SetAsync",
    GetAllPropertiesMethod = "GetAllAsync", WatchPropertiesMethod = "WatchPropertiesAsync")]
public interface IMprisPlayer : IDBusObject
{
    Task PlayAsync();
    Task PauseAsync();
    Task PlayPauseAsync();
    Task StopAsync();
    Task NextAsync();
    Task PreviousAsync();

    /// <param name="offsetUs">Offset relativo en microsegundos.</param>
    Task SeekAsync(long offsetUs);

    /// <param name="trackId">TrackId devuelto en Metadata.mpris:trackid.</param>
    /// <param name="positionUs">Posición absoluta en microsegundos.</param>
    Task SetPositionAsync(ObjectPath trackId, long positionUs);

    Task OpenUriAsync(string uri);

    Task<object> GetAsync(string property);
    Task SetAsync(string property, object value);
    Task<IDictionary<string, object>> GetAllAsync();
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);

    /// <summary>Suscripción a la señal Seeked. Invocar el handler emite la señal.</summary>
    Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null);
}

/// <summary>
/// Implementación MPRIS2 expuesta en el bus de sesión. Singleton efectivo:
/// una sola instancia por proceso. La implementación D-Bus (interfaces,
/// atributos, signatures, codegen) no se modifica respecto del original.
/// </summary>
public class MprisService : IMprisRoot, IMprisPlayer
{
    public ObjectPath ObjectPath => new(ObjPath);
    private const string ObjPath = "/org/mpris/MediaPlayer2";   // fijo por spec MPRIS2

    class Unsubscriber : IDisposable
    {
        Action _onDispose;
        public Unsubscriber(Action onDispose) => _onDispose = onDispose;
        public void Dispose() { _onDispose?.Invoke(); _onDispose = null; }
    }

    /// <summary>
    /// Devuelve el BusName a usar. Si "org.mpris.MediaPlayer2.{name}" está libre,
    /// lo devuelve tal cual. Si está tomado, le pega el PID al final del nombre.
    /// </summary>
    static async Task<string> ResolveBusNameAsync(Connection conn, string name)
    {
        string baseName = "org.mpris.MediaPlayer2." + name;

        // Preguntar al bus si el nombre ya tiene owner.
        // Hablamos directo con org.freedesktop.DBus.NameHasOwner.
        const string dbusService = "org.freedesktop.DBus";
        var dbusPath = new ObjectPath("/org/freedesktop/DBus");
        var dbusIface = conn.CreateProxy<IDBus>(dbusService, dbusPath);
        bool taken = await dbusIface.NameHasOwnerAsync(baseName);

        return taken ? baseName + Environment.ProcessId : baseName;
    }

    // Proxy mínimo para hablar con org.freedesktop.DBus (solo lo que necesitamos).
    [DBusInterface("org.freedesktop.DBus")]
    public interface IDBus : IDBusObject
    {
        Task<bool> NameHasOwnerAsync(string name);
    }

    static Connection connection;
    static MprisService instance;
    public static MprisSource source { get; set; }
    public static MprisCapabilities capabilities { get; private set; }
    static MprisOptions options;

    // Snapshot de lo último emitido, para detectar cambios reales en Update()
    public static string lastCoverFile { get; private set; } = "";
    static ObjectPath trackId;
    public static string shownTitle { get; private set; } = "";
    public static string shownArtist { get; private set; } = "";
    public static bool shownPlaying { get; private set; }
    public static string lastCoverHash { get; private set; } = "";
    public static string coverUri { get; private set; }
    public static string shownLoopStatus { get; private set; }
    public static bool shownShuffle { get; private set; }
    public static double shownVolume { get; private set; } = 1.0;
    public static double shownRate { get; private set; } = 1.0;
    public static long shownPositionUs { get; private set; } = 0;

    ///<summary> Umbral para emitir Seeked: si la posición cambió más de esto entre
    /// llamadas a Update(), asumimos que fue un seek (no avance natural). </summary>
    public static long SeekThresholdUs { get; set; } = 1_000_000; // 1 segundo

    // Snapshot de capabilities, para detectar cambios en runtime.
    private static bool shownCanQuit, shownCanPlay, shownCanPause,
                shownCanSeek, shownCanGoNext, shownCanGoPrevious, shownCanStop,
                shownCanControl;


    static event Action<PropertyChanges> PlayerChanged;
    static event Action<long> Seeked;

    MprisService() { }

    static string BusName;
    static string Identity => string.IsNullOrEmpty(options.DisplayName) ? options.Name : options.DisplayName;
    static string DesktopEntry => options.Name;
    static string CacheDir => "/tmp/" + options.Name;
    static string TrackIdPrefix => $"/org/{options.Name}/MediaPlayer2/Track/";

    /// <summary>
    /// Registra el servicio MPRIS en el bus de sesión de D-Bus.
    /// </summary>
    /// <param name="src">Fuente de estado y acciones que tu app provee.</param>
    /// <param name="caps">Funcionalidades declaradas por tu app.</param>
    /// <param name="opts">Nombre y display name de la app.</param>
    public static async Task StartAsync(MprisSource src, MprisCapabilities caps, MprisOptions opts)
    {
        source = src;
        capabilities = caps;
        options = opts;

        shownTitle = src.Title;
        shownArtist = src.Artist;
        shownPlaying = src.IsPlaying;
        lastCoverHash = src.CoverHashHex;
        shownLoopStatus = src.LoopStatus;
        shownShuffle = src.Shuffle;
        shownVolume = src.Volume;
        shownRate = src.Rate;
        shownPositionUs = src.PositionUs;
        trackId = new ObjectPath(TrackIdPrefix + "0");

        // Snapshot inicial de capabilities
        shownCanQuit = caps.CanQuit;
        shownCanPlay = caps.CanPlay;
        shownCanPause = caps.CanPause;
        shownCanSeek = caps.CanSeek;
        shownCanGoNext = caps.CanGoNext;
        shownCanGoPrevious = caps.CanGoPrevious;
        shownCanStop = caps.CanStop;
        shownCanControl = caps.CanControl;

        connection = new Connection(Address.Session);
        await connection.ConnectAsync();
        instance = new MprisService();
        await connection.RegisterObjectAsync(instance);

        BusName = await ResolveBusNameAsync(connection, options.Name);
        await connection.RegisterServiceAsync(BusName);
    }

    /// <summary>
    /// Compara el estado actual de <see cref="MprisSource"/> con el último emitido
    /// y, si algo cambió, emite la señal PropertiesChanged por D-Bus.
    /// Llamarlo desde la app cada vez que el estado pueda haber cambiado.
    /// </summary>
    public static void Update()
    {
        if (source == null) return;

        var changed = new Dictionary<string, object>();
        bool trackChanged = false;

        if (source.Title != shownTitle)
        {
            shownTitle = source.Title;
            trackId = new ObjectPath(TrackIdPrefix + Environment.TickCount64);
            trackChanged = true;
        }

        if (source.CoverHashHex != lastCoverHash && !string.IsNullOrWhiteSpace(source.CoverHashHex) && source.CoverBytes?.Length > 0)
        {
            try
            {
                Directory.CreateDirectory(CacheDir);

                if (!string.IsNullOrWhiteSpace(lastCoverFile))
                    File.Delete(lastCoverFile);

                string file = Path.Combine(CacheDir, $"cover-{source.CoverHashHex}.png");
                File.WriteAllBytes(file, source.CoverBytes);
                coverUri = new Uri(file).AbsoluteUri;
                lastCoverFile = file;
            }
            catch { }
            lastCoverHash = source.CoverHashHex;
            changed["Metadata"] = BuildMetadata();
        }
        else if (source.CoverHashHex != lastCoverHash)
        {
            coverUri = null;
            changed["Metadata"] = BuildMetadata();
        }

        if (trackChanged || source.Artist != shownArtist)
        {
            shownArtist = source.Artist;
            changed["Metadata"] = BuildMetadata();
        }

        if (source.IsPlaying != shownPlaying)
        {
            shownPlaying = source.IsPlaying;
            changed["PlaybackStatus"] = shownPlaying ? "Playing" : "Paused";
        }

        if (Math.Abs(source.PositionUs - shownPositionUs) > SeekThresholdUs)
        {
            shownPositionUs = source.PositionUs;
            Seeked?.Invoke(source.PositionUs);
        }
        else
            shownPositionUs = source.PositionUs;

        if (capabilities.SupportsLoop && source.LoopStatus != shownLoopStatus)
        {
            shownLoopStatus = source.LoopStatus;
            changed["LoopStatus"] = shownLoopStatus;
        }
        if (capabilities.SupportsShuffle && source.Shuffle != shownShuffle)
        {
            shownShuffle = source.Shuffle;
            changed["Shuffle"] = shownShuffle;
        }
        if (capabilities.SupportsVolume && source.Volume != shownVolume)
        {
            shownVolume = source.Volume;
            changed["Volume"] = shownVolume;
        }
        if (capabilities.SupportsRate && source.Rate != shownRate)
        {
            shownRate = source.Rate;
            changed["Rate"] = shownRate;
        }

        if (capabilities.CanQuit != shownCanQuit)
        {
            shownCanQuit = capabilities.CanQuit;
            changed["CanQuit"] = shownCanQuit;
        }
        if (capabilities.CanPlay != shownCanPlay)
        {
            shownCanPlay = capabilities.CanPlay;
            changed["CanPlay"] = shownCanPlay;
        }
        if (capabilities.CanPause != shownCanPause)
        {
            shownCanPause = capabilities.CanPause;
            changed["CanPause"] = shownCanPause;
        }
        if (capabilities.CanSeek != shownCanSeek)
        {
            shownCanSeek = capabilities.CanSeek;
            changed["CanSeek"] = shownCanSeek;
        }
        if (capabilities.CanGoNext != shownCanGoNext)
        {
            shownCanGoNext = capabilities.CanGoNext;
            changed["CanGoNext"] = shownCanGoNext;
        }
        if (capabilities.CanGoPrevious != shownCanGoPrevious)
        {
            shownCanGoPrevious = capabilities.CanGoPrevious;
            changed["CanGoPrevious"] = shownCanGoPrevious;
        }
        if (capabilities.CanStop != shownCanStop)
        {
            shownCanStop = capabilities.CanStop;
            changed["CanStop"] = shownCanStop;
        }
        if (capabilities.CanControl != shownCanControl)
        {
            shownCanControl = capabilities.CanControl;
            changed["CanControl"] = shownCanControl;
        }

        if (changed.Count > 0)
            PlayerChanged?.Invoke(new PropertyChanges { Changed = changed, Invalidated = Array.Empty<string>() });
    }

    static IDictionary<string, object> BuildMetadata()
    {
        var m = new Dictionary<string, object> { ["mpris:trackid"] = trackId };
        if (source.Title != null) m["xesam:title"] = source.Title;
        if (source.Artist != null) m["xesam:artist"] = new[] { source.Artist };
        if (source.DurationUs > 0) m["mpris:length"] = source.DurationUs;
        if (coverUri != null) m["mpris:artUrl"] = coverUri;
        return m;
    }

    // ─── MÉTODOS (escritorio → server). Públicos y de instancia porque el
    // codegen de Tmds.DBus los escanea con type.GetMethods(). ───
    public Task PlayAsync() { if (!source.IsPlaying) source.TogglePlayPause(); return Task.CompletedTask; }
    public Task PauseAsync() { if (source.IsPlaying) source.TogglePlayPause(); return Task.CompletedTask; }
    public Task PlayPauseAsync() { source.TogglePlayPause(); return Task.CompletedTask; }
    public Task StopAsync() { source.Stop(); return Task.CompletedTask; }
    public Task NextAsync() { source.Next(); return Task.CompletedTask; }
    public Task PreviousAsync() { source.Prev(); return Task.CompletedTask; }

    public Task SeekAsync(long offsetUs)
    {
        source.Seek(offsetUs);
        Seeked?.Invoke(source.PositionUs);
        return Task.CompletedTask;
    }

    public Task SetPositionAsync(ObjectPath tid, long positionUs)
    {
        if (tid == trackId && positionUs >= 0)
        {
            source.SetPosition(positionUs);
            Seeked?.Invoke(positionUs);
        }
        return Task.CompletedTask;
    }

    public Task OpenUriAsync(string uri) { source.OpenUri(uri); return Task.CompletedTask; }
    public Task RaiseAsync() { source.Raise(); return Task.CompletedTask; }
    public Task QuitAsync() { source.Quit(); return Task.CompletedTask; }

    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
    {
        Seeked += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => Seeked -= handler));
    }

    Task<IDisposable> IMprisRoot.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        PlayerChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => PlayerChanged -= handler));
    }

    Task<IDisposable> IMprisPlayer.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        PlayerChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => PlayerChanged -= handler));
    }

    // PÚBLICOS a propósito: el codegen escanea con type.GetMethods() y los métodos
    // de implementación explícita son invisibles para esa llamada.

    public Task<object> GetAsync(string property)
    {
        var all = GetAllProperties();
        if (!all.TryGetValue(property, out var v))
            throw new InvalidOperationException("No such property: " + property);
        return Task.FromResult(v);
    }

    public Task SetAsync(string property, object value)
    {
        switch (property)
        {
            case "LoopStatus" when capabilities.SupportsLoop:
                source.LoopStatus = (string)value;
                source.LoopChanged(source.LoopStatus);
                break;
            case "Shuffle" when capabilities.SupportsShuffle:
                source.Shuffle = Convert.ToBoolean(value);
                source.ShuffleChanged(source.Shuffle);
                break;
            case "Volume" when capabilities.SupportsVolume:
                source.Volume = Convert.ToDouble(value);
                source.VolumeChanged(source.Volume);
                break;
            case "Rate" when capabilities.SupportsRate:
                source.Rate = Convert.ToDouble(value);
                source.RateChanged(source.Rate);
                break;
        }
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> GetAllAsync()
        => Task.FromResult(GetAllProperties());

    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        PlayerChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => PlayerChanged -= handler));
    }

    static IDictionary<string, object> GetAllProperties()
    {
        var d = new Dictionary<string, object>
        {
            // Root
            ["CanQuit"] = capabilities.CanQuit,
            ["CanRaise"] = capabilities.CanRaise,
            ["HasTrackList"] = false,
            ["Identity"] = Identity,
            ["DesktopEntry"] = DesktopEntry,
            ["SupportedUriSchemes"] = capabilities.SupportedUriSchemes,
            ["SupportedMimeTypes"] = capabilities.SupportedMimeTypes,

            // Player
            ["PlaybackStatus"] = source.IsPlaying ? "Playing" : "Paused",
            ["Metadata"] = BuildMetadata(),
            ["Position"] = source.PositionUs,
            ["CanControl"] = capabilities.CanControl,
            ["CanPlay"] = capabilities.CanPlay,
            ["CanPause"] = capabilities.CanPause,
            ["CanSeek"] = capabilities.CanSeek,
            ["CanGoNext"] = capabilities.CanGoNext,
            ["CanGoPrevious"] = capabilities.CanGoPrevious,
            ["CanStop"] = capabilities.CanStop,
            ["MinimumRate"] = 1.0,
            ["MaximumRate"] = 1.0,
            ["Rate"] = 1.0
        };

        // Rate — siempre presente. Si no se soporta, forzado a 1.0.
        if (capabilities.SupportsRate)
            d["Rate"] = source.Rate;

        // Opcionales
        if (capabilities.SupportsLoop)
            d["LoopStatus"] = source.LoopStatus;

        if (capabilities.SupportsShuffle)
            d["Shuffle"] = source.Shuffle;

        if (capabilities.SupportsVolume)
            d["Volume"] = source.Volume;

        return d;
    }
}
