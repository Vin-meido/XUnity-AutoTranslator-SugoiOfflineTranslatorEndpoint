# XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint

Translation endpoint to support Sugoi Translator's offline translation backend (https://www.youtube.com/watch?v=r8xFzVbmo7k)

Tested to support Sugoi Translator V3.0 with Offline Model V2.0. Supports games that use Unity 5.6+.

The Sugoi Translator's offline model boasts comparability with Deepl translations, not to mention a shorter delay and nonexistent throttling limits.

Though just like any machine translators, there will be oddities and unexpected mistranslations. You're still better off using official/fan translations/localizations if those are available.

Even so, unless you already use SugoiTranslator (and its offline model), you might be better off using the google, deepl, or other official translation endpoints (unless you have the disk space and processing power to spare).


## Requirements

SugoiTranslator's offline mode recommends at least 8GB of RAM. Make sure you have that + the amount of memory the game you're running also requires.

CUDA support requires an NVIDIA graphics card that supports it (GTX20xx, RTX series).


## Installation

1. Install Sugoi Translator if you havent yet, and install the Offline Model. See the youtube (https://www.youtube.com/watch?v=r8xFzVbmo7k) for details on installation and setup. Make sure you have a working translator first by running the offline translator script `Sugoi-Translator-Offline (click here).bat`

**optional**: If you have a recent nvidia graphics card (20xx, RTX series), you can also install the Cuda update from their discord (https://discord.com/channels/778778890239344641/795551389211164703/902472195710779394) for faster translations. Check included docs in the package for install instructions of this update. Make sure that's working as well before continuing.

2. Copy `SugoiOfflineTranslator.dll` and `SugoiOfflineTranslatorServer.py` to XUAT's `translators` folder. This is usual at `BepinEx\plugins\XUnity.AutoTranslator\Translators` from the root/install folder of your game.

3. Backup your XUAT configuration file. This is usually at at `BepinEx\config\AutoTranslatorConfig.ini`. After you have a backup copy, edit the configuration and change the `Endpoint` setting to `SugoiOfflineTranslatorEndpoint`.  Your `[Service]` section should look like this:
```
[Service]
Endpoint=SugoiOfflineTranslatorEndpoint
FallbackEndpoint=
```

4. Add the following additional settings at the end of the configuration:

```
[SugoiOfflineTranslator]
InstallPath=
ServerPort=14367
EnableCuda=False
MaxBatchSize=10
```

Set the `InstallPath` setting to the full path where Sugoi Translator is installed/extracted.  This folder is the folder that contains the various `.bat` batch files to start the different translator modes.

If you installed the CUDA update, set `EnableCuda` to `True` and increase `MaxBatchSize` to a larger value (e.g. `100`)

## Usage

Just run the game. Do not run the SugoiTranslator's offline mode batch script, as the endpoint starts its own version of the server.
