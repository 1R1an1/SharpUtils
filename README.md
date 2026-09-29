
# SharpUtils

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?logo=sharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows-lightgrey)
[![License](https://img.shields.io/badge/license-MPL--2.0-blue)](https://www.mozilla.org/en-US/MPL/2.0/)
[![Last Commit](https://img.shields.io/github/last-commit/1R1an1/SharpUtils)](https://github.com/1R1an1/SharpUtils/commits/master/)
[![Repo Stars](https://img.shields.io/github/stars/1R1an1/SharpUtils?style=social)](https://github.com/1R1an1/SharpUtils)

> Librería de utilidades para mis proyectos en C# / .NET 10.

Reúne funcionalidades sueltas que se repiten entre mis apps, networking con handshake y TLS, criptografía, MPRIS2 para Linux, helpers de archivos y JSON, codecs de audio y compresión, etc.

---

## Requisitos

- .NET SDK **10.0+**
- Linux o Windows
<!--
 | Requisito         | Detalle         |
 | ----------------- | --------------- |
 | .NET SDK          | **10.0+**       |
 | Sistema operativo | Linux - Windows |
 -->
<!-- | Dependencias NuGet | Concentus, K4os.Compression.LZ4.Streams, Tmds.DBus,  ZstdNet | -->
<!-- > Las dependencias se restauran automáticamente con `dotnet restore`. -->
<!-- > La librería es multiplataforma, pero la carpeta Linux/ solo funciona en Linux (usa libc y D-Bus directamente). -->

---

## Funcionalidades

- **Audio**: codificador y decodificador Opus para streaming en tiempo real.
- **Compresión**: LZ4 y Zstd, con helpers para comprimir y descomprimir arrays de bytes.
- **Crypto**: AES-CTR con número de secuencia para paquetes UDP desordenados, AES-GCM con contraseña, y derivación de clave por PBKDF2.
- ***Linux Only***: MPRIS2 completo sobre D-Bus, y acceso a libc para leer directorios con verificación real de permisos.
- **Networking**: servidor y cliente TCP con handshake challenge-response, TLS opcional con certificado autofirmado en memoria, y un protocolo de paquetes con endianness configurable.
- **Utilities**: formato de tamaños y velocidades, hashing de archivos en streaming, obtención de la IP LAN real (ignorando VPN y adaptadores virtuales), y load/save de JSON tipado.

---

## Añadir a un proyecto

SharpUtils se distribuye como código fuente. La forma recomendada es como submódulo de git más un `ProjectReference` en el `.csproj` del proyecto que la consume.

Para añadir SharpUtils como submódulo a un proyecto existente:

```bash
cd <proyecto>
git submodule add https://github.com/1R1an1/SharpUtils.git SharpUtils
```

Y referenciarla desde el `.csproj` del proyecto:

```xml
<ItemGroup>
  <ProjectReference Include="SharpUtils/SharpUtils.csproj" />
</ItemGroup>

<ItemGroup>
  <Compile Remove="SharpUtils\**" />
</ItemGroup>
```

Quien clone el proyecto después, necesita inicializar el submódulo:

```bash
git clone --recursive https://github.com/1R1an1/<proyecto>.git
# o, si ya estaba clonado sin --recursive:
git submodule update --init --recursive
```

---

## Documentación

Toda la API pública está documentada con comentarios XML de C# `/// <summary>`, disponible en el IntelliSense (VS Code, Visual Studio y Rider) al usar la librería. La documentación está en español.

---

## Licencia

Este proyecto está bajo la licencia **MPL 2.0**.
