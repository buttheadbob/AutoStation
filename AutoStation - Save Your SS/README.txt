This project templates provides:

    .gitignore
        - In case you are using Git for version control this file will add a blacklist
        - It is needed to prevent unwanted files to be put under version control.
        - Mainly the GameBinaries and TorchBinaries should never show up in there.
        - As well as your output folders and .vs folder itself.
        - Feel free to add additional files if you need to.

    manifest.xml
        - Defines Plugin name, ID, and Version.
        - Used for publishing on torch website.
        - Also used for display within Torch UI, and loading through Torch.cfg
        - After plugin is published you must not change Name and ID anymore.
        - ID must match plugin page, so you may need to change it.

    AutoStation___Save_Your_SSPlugin.cs
        - Your plugins mainclass.
        - Sets up the UI and config file.
        - Has an example of how to listen to session state changes.

    AutoStation___Save_Your_SSConfig.cs
        - The model of your config file. 
        - Even if you dont have many settings, at least something like "enable" may be useful.
        - Deletion requires changes in other classes.

    AutoStation___Save_Your_SSCommands.cs
        - Excample class on how to create commands.
        - If no commands needed, can be safely deleted.

    AutoStation___Save_Your_SSControl.xaml
        - WPF definiton of your UI
        - Its bounds to your Config, careful of the property names.
        - If you screw them up, Torch can and will likely crash.
        - If no settings needed, you can also display additional information for admins.
                - Like best practices or more advanced command descriptions.

    AutoStation___Save_Your_SSControl.xaml.cs
        - Class that binds your config to the UI.
        - unless you add many fancy buttons or events, you likely never have to change this class.

After project creation

    1. Close project and run provides Setup bat.
        - It asks for the installation directory of SE Dedicated server (can be found in torch). 
            (Inside Torch its the DedicatedServer64 folder)
        - It also asks for Torch root directory
            (The folder where the Torch.Server.exe is located.)
        - Creates a symlink to these folders.
        - Your Plugin will use these simlinks to get the dlls of Torch and Space Engineers.

    2. Open your Project again and check if references are loaded correctly.
        - If not check if simlink is set up correctly, it should be located next to your vsproj file.

    3. Reformat code to your liking.
	- Your IDE should got you covered there

    4. Rename classes to your liking.
        - Torch does not need the class names. It figures out what to load based on their implementation.
        - Classnames may be weird if your project name contains spaces.
        - Avoid generic names like "Commands", because if every plugin does that logfiles may become confusing.

    5. Compile your Project
        - Everything should work fine
        - Your Projects bin folder should contain your dll, pdb and manifest file.
	- Those you can easily drop into your plugin zip. 
	
    6. Start coding
        - Follow the wiki or ask on discord if you have questions or problems.