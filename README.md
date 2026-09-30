<div align="center">

   # convert

</div>

**CLI personal para conversiones rápidas de imágenes, usando FFmpeg como backend.**

[![.NET 9](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Windows x64](https://img.shields.io/badge/platform-Windows%20x64-0078D4?logo=windows&logoColor=white)](https://github.com/TeoMarquez/convert-imageType)
[![FFmpeg](https://img.shields.io/badge/backend-FFmpeg-007808?logo=ffmpeg&logoColor=white)](https://ffmpeg.org/)

## Requisitos

### Para ejecutar

- Windows x64
- FFmpeg disponible en `PATH`, o `FFMPEG_PATH`

### Para compilar

- .NET 9 SDK

## Uso

```powershell
convert foto.png webp
convert .\images png jpg
convert logo.png ico
```

La conversión por carpeta procesa archivos del nivel inmediato. Se omiten las salidas que ya existen.

## Formatos soportados

La CLI reconoce estos formatos para entrada y salida. La conversión concreta depende de que la instalación de FFmpeg incluya el decodificador y el codificador necesarios.

| Formato | Extensiones aceptadas | Extensión de salida |
| --- | --- | --- |
| PNG | `.png` | `.png` |
| JPEG | `.jpg`, `.jpeg` | `.jpg` |
| WebP | `.webp` | `.webp` |
| Bitmap | `.bmp` | `.bmp` |
| GIF | `.gif` | `.gif` |
| TIFF | `.tif`, `.tiff` | `.tif` |
| Windows Icon | `.ico` | `.ico` |

Al convertir una imagen animada, se procesa únicamente el primer cuadro. La entrada `jpeg` se acepta como alias de `jpg`, y `tiff` como alias de `tif`.

ICO se crea como un contenedor real con siete variantes PNG de 16, 24, 32, 48, 64, 128 y 256 píxeles. Requiere un FFmpeg que pueda decodificar la imagen de entrada y codificar PNG.

## Requisitos e instalación en Windows

1. Instala FFmpeg y asegúrate de que `ffmpeg.exe` esté en `PATH`. También puedes definir `FFMPEG_PATH` con la ruta completa al ejecutable.
2. Instala el SDK de .NET 9 para compilar, o descarga/publica el ejecutable autocontenido.
3. Desde esta carpeta, crea la distribución autocontenida para Windows x64:

   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true
   ```

4. Agrega `bin\Release\net9.0\win-x64\publish` (o la carpeta donde copies `convert.exe`) al `PATH` de usuario desde **Editar las variables de entorno de la cuenta**. Abre una terminal nueva y ejecuta `convert --help`.

El ejecutable de la CLI no incluye FFmpeg; FFmpeg se instala por separado. La sintaxis de carpeta usa `convert <carpeta> <formato_origen> <formato_salida>` para que el filtro por formato sea explícito.
