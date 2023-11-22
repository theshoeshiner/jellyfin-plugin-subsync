# Jellyfin SubSync Plugin

This plugin integrates with SubSync to synchronize subtitles with media files.

# Installation

Manifest is up an running! You can now import the manifest in Jellyfin and this plugin will appear in the Catalog!
- Go to "Plugins" on your "Dashboard"
- Go to the "Repositories" tab
- Click the '+' to add a new Repository
    - Give it a name (i.e. Newsletters)
    - In "Repository URL," put "https://raw.githubusercontent.com/theshoeshiner/jellyfin-plugin-subsync/main/manifest.json"
    - Click "Save"
- You should now see "SubSync" in Catalog under the Category "Metadata"
- Once installed, restart Jellyfin to activate the plugin and configure your settings for the plugin

# Configuration

## General Config

### To Addresses:
- Recipients of the newsletter. Add add as many emails as you'd like, separated by commas.
    - All emails will be sent out via BCC

### From Address
- The address recipients will see on emails as the sender
    - Defaults to JellyfinNewsletter@donotreply.com