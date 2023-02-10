# XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint

Translation endpoint to support Sugoi Translator's offline translation backend (https://www.youtube.com/watch?v=r8xFzVbmo7k)

Tested to support Sugoi Translation Toolkit V4.0.

The Sugoi Translator's offline model boasts comparability with Deepl translations, not to mention shorter translation delay and nonexistent throttling limits.

Though just like any machine translators, there will be oddities and unexpected mistranslations. You're still better off using official/fan translations/localizations if those are available.

Even so, unless you already use SugoiTranslator (and its offline model), you might be better off using the google, deepl, or other official translation endpoints (unless you have the disk space and processing power to spare).


## Requirements

XUnity.AutoTranslator 5.0.0 or newer.

SugoiTranslator's offline mode recommends at least 8GB of RAM. Make sure you have that + the amount of memory the game you're running also requires.

CUDA support requires an NVIDIA graphics card that supports it (GTX10xx, RTX series).


## Installation

0. Install XUAT. See https://github.com/bbepis/XUnity.AutoTranslator#installation for installation instructions, and then get that working with the default translators before proceeding.

1. Install Japanese OCR Toolkit. See https://www.youtube.com/watch?v=r8xFzVbmo7k for details on installation and setup. Make sure you have a working translator first by running the offline translator script `Sugoi-Translator-Offline (click here).bat`

**optional**: If you wish to have faster translations, you can optionally install either or both CUDA and/or ctranslate2 support for sugoi. Check the Visual Novel OCR discord for setup instructions (https://discord.com/channels/778778890239344641/906562033397407824/1033742010118570074). Note that CUDA requires a compatible NVIDIA graphics card.

2. Get the latest `SugoiOfflineTranslator.dll` file from the latest release: https://github.com/Vin-meido/XUnity-AutoTranslator-SugoiOfflineTranslatorEndpoint/releases/latest/. Save it in XUAT's `Translators` folder. The location of this folder depends on the loader you are using to use for XUAT. Consult XUAT's installation instructions as to where to expect this folder is located at.

3. Run your game once to generate/update the XUAT configuration file. Once the game has run and initialized properly, exit the game.

4. Backup your XUAT configuration file (`AutoTranslatorConfig.ini`). After you have a backup copy, edit the configuration and change the `Endpoint` setting to `SugoiOfflineTranslator`.  Your `[Service]` section should look like this:
```
[Service]
Endpoint=SugoiOfflineTranslator
FallbackEndpoint=
```

5. **(optional)** Go to the `[SugoiOfflineTranslator]` section of the configuration and set the `InstallPath` setting to the full path where Sugoi Translator is installed/extracted.  This folder is the folder that contains the various `.bat` batch files to start the different translator modes.

If you installed the CUDA support, set `EnableCuda` to `True` and increase `MaxBatchSize` to a larger value (e.g. `100`).

If you installed ctranslate2 support, set `EnableCtranslate` to `True`.

Optionally, if you want the translations to reflect faster, set `EnableShortDelay` to `True`. There's a bunch more configuration options you can set (refer to the configuration section for details on what they do)


## Usage

Run the game. If you set the `InstallPath` setting, do not run the SugoiTranslator's offline mode batch script, as the endpoint starts its own version of the server.

Once the game is running you can press `Alt`+`0` to bring up the XUAT panel to confirm that you've configured the endpoint properly and if it's translating.


## Updating

The translator endpoint may be updated by just extracting / overwriting the old plugin based on your installation. Though there may be additional steps based on what version you are upgrading from:

### Versions older than 1.4.0

Versions prior to 1.4.0 had an optional instruction to install XUATHooks. As of XUAT 4.21.0 / endpoint version 1.4.0, this is no longer needed. Remove the old XUATHooks dll when updating, and then set `EnableShortDelay` to `True` in the updated configuration.

### Versions older than 1.2.0

Versions prior to 1.2.0 had instructions to extract SugoiOfflineTranslatorServer.py together with the translator dll. This is no longer needed and removing the old server.py file can be done.


## Configuration

`InstallPath`: The location of your Sugoi Translator install. When set, automatically starts the translation backend internally when you start the game. This must be set to the folder that contains the various `.bat` batch files to start the different translator modes.

`ServerPort`: Dedicated port to use for the internal backend endpoint.

`EnableCuda`: Enables CUDA / graphics card acceleration for translation. Set to True if you installed the CUDA extensions for Sugoi Translator

`EnableCTranslate2`: Enables ctranslate2 accelaration. Can be enabled without CUDA (sugoi needs to have ctranslate2 installed).

`MaxBatchSize`: Sets the maximum amount of untranslated lines to send to the translator per batch. If your pc specifications can handle it, you can set it to a high value (100). However, the default should work fine in most cases.

`CustomServerScriptPath`: Sets the custom script server to use if you want to use your own or customize the backend server script.

`LogServerMessages`: Logs the backend server's messages into the console of your game (if your loader has it enabled). Useful when reporting a problem (or when you want to see how fast each line is getting translated).

`EnableShortDelay`: Reduces the 0.9s delay used by XUAT to throttle translation requests. This results in making the translations reflect faster (if your pc can handle it). If your game has scrolling text (e.g. dialog/message windows), make sure to set them to as fast as possible to avoid sending multiple requests for partial text. Disable this if you are having issues with fast scrolling / changing text.

`DisableSpamChecks`: Disables the general spam checks associated with online translators (since this is an offline backend, we don't necessarily need it). If your pc cannot handle too many translations requests, you can disable this, but the default should be fine for most setups.
