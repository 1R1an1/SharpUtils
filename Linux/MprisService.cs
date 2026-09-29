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
/// Heredá y asigná los campos desde la subclase; overrideá los métodos que tu app soporte.
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

    /// <summary>Overrideá para responder a play/pause desde el escritorio.</summary>
    protected internal virtual void TogglePlayPause() { }

    /// <summary>Seek relativo. Overrideá si tu app soporta seek.</summary>
    /// <param name="offsetUs">Offset relativo en microsegundos (positivo o negativo).</param>
    protected internal virtual void Seek(long offsetUs) { }

    /// <summary>Seek absoluto. Overrideá si tu app soporta seek.</summary>
    /// <param name="positionUs">Posición absoluta en microsegundos.</param>
    protected internal virtual void SetPosition(long positionUs) { }

    /// <summary>Overrideá si tu app puede detener la reproducción.</summary>
    protected internal virtual void Stop() { }

    /// <summary>Overrideá si tu app puede ir al track siguiente.</summary>
    protected internal virtual void Next() { }

    /// <summary>Overrideá si tu app puede ir al track anterior.</summary>
    protected internal virtual void Prev() { }

    /// <summary>Overrideá si tu app puede cerrarse desde el escritorio.</summary>
    protected internal virtual void Quit() { }

    /// <summary>Overrideá si tu app puede abrir/enfocar su ventana desde el escritorio.</summary>
    protected internal virtual void Raise() { }

    /// <summary>Overrideá si tu app puede abrir URIs externos.</summary>
    /// <param name="uri">URI a abrir.</param>
    protected internal virtual void OpenUri(string uri) { }

    /// <summary>"None", "Track" o "Playlist". Asignar solo si <see cref="MprisCapabilities.SupportsLoop"/> es true.</summary>
    public string LoopStatus = "None";

    /// <summary>Asignar solo si <see cref="MprisCapabilities.SupportsShuffle"/> es true.</summary>
    public bool Shuffle = false;

    /// <summary>Rango 0.0 a 1.0. Asignar solo si <see cref="MprisCapabilities.SupportsVolume"/> es true.</summary>
    public double Volume = 1.0;

    /// <summary>Velocidad de reproducción. Asignar solo si <see cref="MprisCapabilities.SupportsRate"/> es true.</summary>
    public double Rate = 1.0;

    /// <summary>Se invoca cuando el escritorio cambia <see cref="LoopStatus"/>.</summary>
    /// <param name="loop">Nuevo valor de <see cref="LoopStatus"/> ("None", "Track" o "Playlist").</param>
    protected internal virtual void LoopChanged(string loop) { }

    /// <summary>Se invoca cuando el escritorio cambia <see cref="Shuffle"/>.</summary>
    /// <param name="shuffle">Nuevo valor de <see cref="Shuffle"/>.</param>
    protected internal virtual void ShuffleChanged(bool shuffle) { }

    /// <summary>Se invoca cuando el escritorio cambia <see cref="Volume"/>.</summary>
    /// <param name="volume">Nuevo valor de <see cref="Volume"/> (0.0 a 1.0).</param>
    protected internal virtual void VolumeChanged(double volume) { }

    /// <summary>Se invoca cuando el escritorio cambia <see cref="Rate"/>.</summary>
    /// <param name="rate">Nuevo valor de <see cref="Rate"/>.</param>
    protected internal virtual void RateChanged(double rate) { }
}

/// <summary>
/// Declara qué funcionalidades soporta tu app ante MPRIS.
/// Los <c>CanX</c> controlan la UI del escritorio; los <c>SupportsX</c> habilitan props opcionales.
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

    /// <summary>Si la app puede detener (distinto de pausar).</summary>
    public bool CanStop { get; set; } = false;

    /// <summary>Si el escritorio puede mandar comandos en absoluto.</summary>
    public bool CanControl { get; init; } = true;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.LoopStatus"/>.</summary>
    public bool SupportsLoop { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Shuffle"/>.</summary>
    public bool SupportsShuffle { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Volume"/>.</summary>
    public bool SupportsVolume { get; init; } = false;

    /// <summary>Habilita lectura/escritura de <see cref="MprisSource.Rate"/>.</summary>
    public bool SupportsRate { get; init; } = false;

    /// <summary>Esquemas URI que la app acepta en <see cref="MprisSource.OpenUri"/>.</summary>
    public string[] SupportedUriSchemes { get; init; } = Array.Empty<string>();

    /// <summary>Tipos MIME que la app puede abrir.</summary>
    public string[] SupportedMimeTypes { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Configuración de la app para MPRIS.
/// Solo nombre y display name; el resto se deriva automáticamente.
/// </summary>
public sealed record class MprisOptions
{
    private string _name;

    /// <summary>Nombre interno de la app. Se normaliza a minúsculas sin espacios.</summary>
    public required string Name { get => _name; init => _name = value.Replace(" ", "").ToLowerInvariant(); }

    /// <summary>Nombre visible en el widget MPRIS. Si está vacío, se usa <see cref="Name"/>.</summary>
    public string DisplayName { get; init; } = "";
}

/// <summary>Payload de la señal PropertiesChanged de D-Bus.</summary>
public struct PropertyChanges
{
    /// <summary>Props cambiadas con sus nuevos valores.</summary>
    public IDictionary<string, object> Changed;

    /// <summary>Props invalidadas (el cliente debe releer con GetAll).</summary>
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
/// Implementación MPRIS2 expuesta en el bus de sesión. Singleton efectivo.
/// </summary>
public class MprisService : IMprisRoot, IMprisPlayer
{
    /// <summary>Proxy mínimo para hablar con org.freedesktop.DBus.</summary>
    [DBusInterface("org.freedesktop.DBus")]
    public interface IDBus : IDBusObject
    {
        /// <param name="name">Nombre de bus a verificar.</param>
        /// <returns>true si el nombre tiene owner.</returns>
        Task<bool> NameHasOwnerAsync(string name);
    }

    /// <summary>Token de desuscripción para los Watch*Async.</summary>
    private sealed class Unsubscriber : IDisposable
    {
        Action _onDispose;
        public Unsubscriber(Action onDispose) => _onDispose = onDispose;
        public void Dispose() { _onDispose?.Invoke(); _onDispose = null; }
    }

    public ObjectPath ObjectPath => new(ObjPath);
    const string ObjPath = "/org/mpris/MediaPlayer2";

    static Connection connection;
    static MprisService instance;

    /// <summary>Source actual asignado por la app.</summary>
    public static MprisSource source { get; set; }

    /// <summary>Capabilities actuales asignadas por la app.</summary>
    public static MprisCapabilities capabilities { get; private set; }
    static MprisOptions options;
    static ObjectPath trackId;

    /// <summary>Último archivo de tapa cacheado.</summary>
    public static string lastCoverFile { get; private set; } = "";

    /// <summary>Último título emitido.</summary>
    public static string shownTitle { get; private set; } = "";

    /// <summary>Último artista emitido.</summary>
    public static string shownArtist { get; private set; } = "";

    /// <summary>Último estado de reproducción emitido.</summary>
    public static bool shownPlaying { get; private set; }

    /// <summary>Último hash de tapa emitido.</summary>
    public static string lastCoverHash { get; private set; } = "";

    /// <summary>Última URI de tapa emitida.</summary>
    public static string coverUri { get; private set; }

    /// <summary>Último LoopStatus emitido.</summary>
    public static string shownLoopStatus { get; private set; }

    /// <summary>Último Shuffle emitido.</summary>
    public static bool shownShuffle { get; private set; }

    /// <summary>Último Volume emitido.</summary>
    public static double shownVolume { get; private set; } = 1.0;

    /// <summary>Último Rate emitido.</summary>
    public static double shownRate { get; private set; } = 1.0;

    /// <summary>Última posición emitida vía Seeked.</summary>
    public static long shownPositionUs { get; private set; } = 0;

    /// <summary>
    /// Umbral para emitir Seeked: si la posición cambió más de esto entre
    /// llamadas a Update(), se asume que fue un seek (no avance natural).
    /// </summary>
    public static long SeekThresholdUs { get; set; } = 1_000_000;

    private static bool shownCanQuit, shownCanPlay, shownCanPause,
                shownCanSeek, shownCanGoNext, shownCanGoPrevious, shownCanStop,
                shownCanControl;

    static event Action<PropertyChanges> PlayerChanged;
    static event Action<long> Seeked;

    private MprisService() { }

    static string BusName;
    static string Identity => string.IsNullOrEmpty(options.DisplayName) ? options.Name : options.DisplayName;
    static string DesktopEntry => options.Name;
    static string CacheDir => "/tmp/" + options.Name;
    static string TrackIdPrefix => $"/org/{options.Name}/MediaPlayer2/Track/";

    /// <summary>Registra el servicio MPRIS en el bus de sesión de D-Bus.</summary>
    /// <param name="src">Fuente de estado y acciones.</param>
    /// <param name="caps">Capabilities declaradas por la app.</param>
    /// <param name="opts">Nombre y display name.</param>
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

    /// <summary>Resuelve el BusName único, agregando el PID si el base está tomado.</summary>
    /// <param name="conn">Conexión D-Bus activa.</param>
    /// <param name="name">Nombre base de la app.</param>
    /// <returns>BusName final a registrar.</returns>
    static async Task<string> ResolveBusNameAsync(Connection conn, string name)
    {
        string baseName = "org.mpris.MediaPlayer2." + name;
        var dbusIface = conn.CreateProxy<IDBus>("org.freedesktop.DBus", new ObjectPath("/org/freedesktop/DBus"));
        bool taken = await dbusIface.NameHasOwnerAsync(baseName);
        return taken ? baseName + Environment.ProcessId : baseName;
    }

    /// <summary>
    /// Emite PropertiesChanged si el estado del source o las capabilities cambiaron.
    /// Llamar desde la app cada vez que cualquier estado pueda haber cambiado.
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

    /// <param name="offsetUs">Offset relativo en microsegundos.</param>
    public Task SeekAsync(long offsetUs)
    {
        source.Seek(offsetUs);
        Seeked?.Invoke(source.PositionUs);
        return Task.CompletedTask;
    }

    /// <param name="tid">TrackId devuelto en Metadata.mpris:trackid.</param>
    /// <param name="positionUs">Posición absoluta en microsegundos.</param>
    public Task SetPositionAsync(ObjectPath tid, long positionUs)
    {
        if (tid == trackId && positionUs >= 0)
        {
            source.SetPosition(positionUs);
            Seeked?.Invoke(positionUs);
        }
        return Task.CompletedTask;
    }

    /// <param name="uri">URI a abrir.</param>
    public Task OpenUriAsync(string uri) { source.OpenUri(uri); return Task.CompletedTask; }
    public Task RaiseAsync() { source.Raise(); return Task.CompletedTask; }
    public Task QuitAsync() { source.Quit(); return Task.CompletedTask; }

    /// <param name="handler">Handler invocado al emitirse Seeked.</param>
    /// <param name="onError">Handler de errores opcional.</param>
    public Task<IDisposable> WatchSeekedAsync(Action<long> handler, Action<Exception> onError = null)
    {
        Seeked += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => Seeked -= handler));
    }

    /// <param name="handler">Handler invocado al cambiar props del root.</param>
    Task<IDisposable> IMprisRoot.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        PlayerChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => PlayerChanged -= handler));
    }

    /// <param name="handler">Handler invocado al cambiar props del player.</param>
    Task<IDisposable> IMprisPlayer.WatchPropertiesAsync(Action<PropertyChanges> handler)
    {
        PlayerChanged += handler;
        return Task.FromResult<IDisposable>(new Unsubscriber(() => PlayerChanged -= handler));
    }

    /// <param name="property">Nombre de la prop a leer.</param>
    public Task<object> GetAsync(string property)
    {
        var all = GetAllProperties();
        if (!all.TryGetValue(property, out var v))
            throw new InvalidOperationException("No such property: " + property);
        return Task.FromResult(v);
    }

    /// <param name="property">Nombre de la prop a escribir.</param>
    /// <param name="value">Nuevo valor.</param>
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

    public Task<IDictionary<string, object>> GetAllAsync() => Task.FromResult(GetAllProperties());

    /// <param name="handler">Handler invocado al cambiar props.</param>
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

        if (capabilities.SupportsRate) d["Rate"] = source.Rate;
        if (capabilities.SupportsLoop) d["LoopStatus"] = source.LoopStatus;
        if (capabilities.SupportsShuffle) d["Shuffle"] = source.Shuffle;
        if (capabilities.SupportsVolume) d["Volume"] = source.Volume;

        return d;
    }
}
