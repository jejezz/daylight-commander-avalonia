using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Localization;

/// <summary>English strings. Buttons and menus are Title Case, sentences are Sentence case (localization.md §7).</summary>
public sealed class EnglishStrings : IAppStrings
{
    public static readonly EnglishStrings Instance = new();

    public string CommonCancel => "Cancel";
    public string CommonConfirm => "OK";

    public string AppBarNewWindow => "New Window";
    public string SettingsTooltip => "Settings";
    public string TreeTooltip => "Directory Tree (Ctrl+T)";

    public string SettingsWindowTitle(string appName) => $"{appName} Settings";
    public string SettingsSectionAppearance => "Appearance";
    public string SettingsMode => "Mode";
    public string SettingsModeSystem => "System";
    public string ThemeLight => "Light";
    public string ThemeDark => "Dark";
    public string SettingsPalette => "Color scheme";
    public string PaletteSaturn => "Saturn";
    public string PaletteTeal => "Teal";
    public string PaletteAmber => "Amber";
    public string PaletteRose => "Rose";
    public string PaletteSlate => "Slate";
    public string PaletteHighContrast => "High contrast";
    public string SettingsFont => "Font";
    public string FontDefaultOption(string name) => $"{name} (default)";
    public string FontSearchHint => "Search fonts";
    public string SettingsFontSize => "Text size";
    public string SettingsFontSizeHint => "Size of all text: menus, tabs, folder names, status bar.";
    public string SettingsLanguage => "Language";
    public string LanguageSystemShort => "System";

    public string SettingsSectionEditor => "Editor";
    public string EditorTitle => "Text editor (F4)";
    public string EditorBuiltin => "Built-in editor (default)";
    public string EditorExternal => "External program";
    public string EditorNotReady => "F4 opens files in the built-in editor.";
    public string EditorExternalHint => "F4 opens the file under the cursor with this program.";
    public string EditorExecutableNone => "No program selected";
    public string EditorChoose => "Browse…";
    public string EditorClear => "Clear";
    public string EditorChooseTitle => "Choose an editor";

    public string PaneLoading => "Loading…";
    public string PaneEmpty => "Empty";
    public string PaneNoMatches => "No matches";
    public string PaneOpenError(string error) => $"Can't open: {error} (click to dismiss)";
    public string PaneItemCount(int count) => count == 1 ? "1 item" : $"{count} items";
    public string PaneSelectedCount(int count) => $"{count} selected";
    public string PaneFilter(string query) => $"Filter: {query}";
    public string PaneBackTooltip => "Back";
    public string PaneForwardTooltip => "Forward";
    public string PaneUpTooltip => "Parent Folder";
    public string PaneRefreshTooltip => "Refresh";
    public string PaneShowHiddenTooltip => "Show Hidden Files";
    public string PaneViewTooltip => "View";
    public string PaneViewList => "List";
    public string PaneViewSmallThumbs => "Small Previews";
    public string PaneViewLargeThumbs => "Large Previews";
    public string DriveTooltip => "Drives";
    public string ColumnName => "Name";
    public string ColumnSize => "Size";
    public string ColumnModified => "Modified";

    public string BookmarkTooltip => "Bookmarks";
    public string BookmarkNone => "No bookmarks";
    public string BookmarkRemoveTooltip => "Remove Bookmark";
    public string BookmarkAddCurrent => "Add current folder to bookmarks";
    public string BookmarkRemoveCurrent => "Remove current folder from bookmarks";
    public string RecentTooltip => "Recent";
    public string RecentNone => "No recent folders";
    public string RecentClear => "Clear Recent";

    public string FnRename => "F2 Rename";
    public string FnView => "F3 View";
    public string FnEdit => "F4 Edit";
    public string FnCopy => "F5 Copy";
    public string FnMove => "F6 Move";
    public string FnNewFolder => "F7 New Folder";
    public string FnDelete => "F8 Delete";
    public string FnNewFile => "New File (Ctrl+N)";
    public string FnCompress => "Compress";
    public string FnExtract => "Extract";
    public string FnProperties => "Properties";
    public string FnPatternSelect => "Pattern Select";

    public string DialogRenameTitle => "Rename";
    public string DialogNewFolderTitle => "New Folder";
    public string DialogNewFileTitle => "New File";
    public string TransferCopyTitle => "Copy";
    public string TransferMoveTitle => "Move";
    public string DropMovePrompt(string what, string destination) => $"Move {what} to this folder?\n{destination}";
    public string TransferPrompt(FileOperationKind kind, string what) =>
        kind == FileOperationKind.Copy ? $"Copy {what} to:" : $"Move {what} to:";
    public string DeleteTrashTitle => "Move to Trash";
    public string DeletePermanentTitle => "Delete Permanently";
    public string DeleteTrashPrompt(string what) => $"Move {what} to the trash?";
    public string DeletePermanentPrompt(string what) => $"Permanently delete {what}? This can't be undone.";
    public string DeleteMore(int count) => $"… and {count} more";
    public string DeleteTrashAction => "Move to Trash";
    public string DeletePermanentAction => "Delete";

    public string ItemsWhat(int count, string firstName) => count == 1 ? $"'{firstName}'" : $"{count} items";

    public string ConflictTitle => "That name already exists";
    public string ConflictExisting => "Existing";
    public string ConflictNew => "New";
    public string ConflictApplyAll => "Apply to all remaining items";
    public string ConflictOverwrite => "Overwrite";
    public string ConflictKeepBoth => "Keep Both";
    public string ConflictSkip => "Skip";
    public string ConflictFolderInfo(string modified) => $"Folder · {modified}";
    public string ConflictInfoUnavailable => "Info unavailable";

    public string JobsClearFinished => "Clear Finished";
    public string JobsCancel => "Cancel";
    public string JobQueued => "Queued";
    public string JobRunning => "Running";
    public string JobDone => "Done";
    public string JobFailed => "Failed";
    public string JobCancelled => "Cancelled";

    public string JobTitle(JobLabel label)
    {
        var what = ItemsWhat(label.Count, label.FirstName);
        return label.Kind switch
        {
            FileOperationKind.Copy => $"Copy · {what} → {label.Target}",
            FileOperationKind.Move => $"Move · {what} → {label.Target}",
            _ => (label.ToTrash ? "Trash · " : "Delete · ") + what,
        };
    }

    public string JobFailureSummary(IReadOnlyList<OperationFailure> failures, FileError? error)
    {
        if (failures.Count == 0) return error is null ? "" : FileErrorText(error);
        var first = failures[0];
        var name = Path.GetFileName(first.Path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : first.Path;
        var more = failures.Count > 1 ? $" and {failures.Count - 1} more" : "";
        return $"{failures.Count} failed: {name} — {FileErrorText(first.Error)}{more}";
    }

    public string ViewerNormalView => "Normal View";
    public string ViewerHexView => "View as Hex";
    public string ViewerZoomIn => "Zoom in";
    public string ViewerZoomOut => "Zoom out";
    public string ViewerZoomFit => "Fit to window (double-click)";
    public string ViewerImageHint => "Zoom with the mouse wheel, trackpad scroll or pinch · drag to pan · double-click to fit";
    public string ViewerUnsupported => "This file type can't be previewed.";
    public string ViewerOpenWithDefaultApp => "Open with Default App";
    public string ViewerLoadError(string error) => $"Couldn't read the file: {error}";
    public string ViewerImageLoadError(string error) => $"Couldn't open the image: {error}";
    public string ViewerHexTruncatedNote(int kb) => $"Showing only the first {kb} KB.";
    public string ViewerTextTruncatedNote(int kb) => $"Large file: showing only the first {kb} KB.";

    public string EditorSave => "Save";
    public string EditorSaveAs => "Save As";
    public string EditorDontSave => "Don't Save";
    public string EditorUnsavedTitle => "Unsaved changes";
    public string EditorUnsavedBody(string name) => $"Save the changes to {name}?";
    public string EditorExternalChangedTitle => "File changed";
    public string EditorExternalChangedBody => "The file on disk changed after you opened it. Overwrite it?";
    public string EditorOverwrite => "Overwrite";
    public string EditorUploadFailed(string error, string tempPath) => $"Couldn't upload to the server: {error}\nYour changes are kept in this temporary file:\n{tempPath}";
    public string EditorRemoteChangedBody => "The file on the server changed after you opened it. Overwrite it?";
    public string EditorSaveFailed(string error) => $"Couldn't save: {error}";
    public string EditorLoadFailed(string error) => $"Couldn't open the file: {error}";
    public string EditorFind => "Find";
    public string EditorFindHint => "Find";
    public string EditorReplaceHint => "Replace with";
    public string EditorReplace => "Replace";
    public string EditorReplaceAll => "Replace All";
    public string EditorMatchCase => "Match case";
    public string EditorRegex => "Regular expression";
    public string EditorPrevMatch => "Previous";
    public string EditorNextMatch => "Next";
    public string EditorToggleReplace => "Toggle Replace";
    public string EditorNoMatches => "No matches";
    public string EditorMatchCount(int current, int total) => $"{current}/{total}";
    public string EditorWordWrap => "Word Wrap";
    public string EditorGoToLine => "Go to Line";
    public string EditorGoToLineHint(int max) => $"Line number (1–{max})";
    public string EditorStatusPosition(int line, int column) => $"Ln {line}, Col {column}";
    public string EditorStatusSelected(int count) => $"{count} selected";
    public string EditorHighlightOff => "Syntax highlighting off (large file)";
    public string EditorSaveAsHint => "Path to save to (a bare name uses the same folder)";
    public string EditorOverwriteExistingBody(string name) => $"{name} already exists. Overwrite it?";
    public string EditorReadOnlyBinary => "This file is binary or not UTF-8. It opens read-only, showing each byte as one character.";
    public string EditorUndo => "Undo";
    public string EditorRedo => "Redo";
    public string EditorMenuView => "View";
    public string EditorCut => "Cut";
    public string EditorCopy => "Copy";
    public string EditorPaste => "Paste";
    public string EditorPreviewTooltip => "Markdown Preview";
    public string EditorGroupFile => "File";
    public string EditorGroupEdit => "Edit";
    public string EditorGroupFind => "Find";
    public string EditorScClose => "Close";
    public string EditorScSelectAll => "Select all";
    public string EditorScDeleteLine => "Delete line";

    public string AboutTooltip => "About";
    public string AboutTagline => "A free dual-pane desktop file manager in the style of Total Commander and Midnight Commander";
    public string AboutDescription => "A classic file manager built for fast keyboard-driven work, with a built-in viewer and editor and room for several windows side by side.";
    public string AboutVersion(string version, int build) => $"Version {version} (build {build})";
    public string AboutOpenSourceLicenses => "Open Source Licenses";
    public string AboutRepository => "GitHub";
    public string AboutClose => "Close";

    // Updates
    public string UpdateCheckButton => "Check for Updates";
    public string UpdateChecking => "Checking for updates…";
    public string UpdateAvailableTitle => "A new version is available";
    public string UpdateAvailableBody(string appName, string current, string latest) => $"{appName} {latest} is available. You have {current}.";
    public string UpdateReleaseNotes => "What's new";
    public string UpdateNow => "Update now";
    public string UpdateLater => "Later";
    public string UpdateSkipVersion => "Skip this version";
    public string UpdateDownloading => "Downloading…";
    public string UpdateVerifying => "Verifying the file…";
    public string UpdateDownloadProgress(string received, string total) => $"{received} / {total}";
    public string UpdateCancel => "Cancel";
    public string UpdateFailedTitle => "Couldn't update";
    public string UpdateCheckFailedTitle => "Couldn't check for updates";
    public string UpdateErrorNetwork => "Couldn't reach the update server. Check your internet connection and try again.";
    public string UpdateErrorChecksum => "The downloaded file didn't pass verification, so it was not installed. Please try again later.";
    public string UpdateErrorInstall => "Couldn't start the installation.";
    public string UpdateErrorGeneric => "The update server sent an unexpected response. Please try again later.";
    public string UpdateUpToDateTitle => "You're up to date";
    public string UpdateUpToDateBody(string version) => $"You're using version {version}.";
    public string UpdateUnavailableTitle => "Install it manually";
    public string UpdateUnavailableBody(string latest) => $"Version {latest} is available, but the app can't install it automatically. Please download it from the release page.";
    public string UpdateOpenReleasePage => "Open release page";
    public string UpdateMacosOpenedTitle => "The installer window is open";
    public string UpdateMacosOpenedBody(string appName) => $"In the window that opened, drag {appName} to the Applications folder. If it's running, quit it first and replace the old copy.";
    public string UpdateWindowsInstallTitle => "The installer is open";
    public string UpdateWindowsInstallBody(string appName) => $"{appName} must quit before it can be updated. Follow the installer's instructions.";
    public string UpdateQuitApp(string appName) => $"Quit {appName}";
    public string UpdateLinuxInstallTitle => "Ready to install";
    public string UpdateLinuxInstallBody(string appName) => $"{appName} will quit and install the update, then start again automatically.";
    public string UpdateQuitAndInstall => "Quit and install";
    public string UpdateBusyTitle => "Can't update right now";
    public string UpdateBusyBody => "A task is in progress. Please try again when it has finished.";
    public string LicensesTitle => "Open Source Licenses";
    public string LicensesProject => "This project";
    public string LicensesNote => "Full license texts and copyright notices are available in each project's repository.";
    public string PropTitle(string name) => $"{name} Properties";
    public string PropMultiName(int count) => $"{count} items";
    public string PropName => "Name";
    public string PropLocation => "Location";
    public string PropKind => "Type";
    public string PropKindFolder => "Folder";
    public string PropKindFile => "File";
    public string PropKindFileExt(string ext) => $"{ext} file";
    public string PropKindMixed => "Multiple types";
    public string PropLinkTarget => "Link target";
    public string PropSize => "Size";
    public string PropSizeValue(string formatted, long bytes) => $"{formatted} ({bytes:N0} bytes)";
    public string PropContains => "Contains";
    public string PropContainsValue(int files, int folders) => $"{files:N0} files, {folders:N0} folders";
    public string PropCreated => "Created";
    public string PropModified => "Modified";
    public string PropAccessed => "Accessed";
    public string PropAttributes => "Attributes";
    public string PropReadOnly => "Read-only";
    public string PropHidden => "Hidden";
    public string PropSystem => "System";
    public string PropCalculating => "Calculating…";
    public string PropIncomplete => "Some items couldn't be read and were left out";

    public string ContextOpen => "Open";
    public string ContextOpenWithDefault => "Open with Default App";
    public string ContextView => "View (F3)";
    public string ContextEdit => "Edit (F4)";
    public string ContextRename => "Rename (F2)";
    public string ContextCopy => "Copy to Other Pane (F5)";
    public string ContextDuplicate => "Duplicate Here (Ctrl+D)";
    public string ContextMove => "Move to Other Pane (F6)";
    public string ContextDelete => "Delete (F8)";
    public string ContextProperties => "Properties (Alt+Enter)";
    public string ContextBookmark => "Add to Bookmarks";
    public string TreeMenuCut => "Cut";
    public string TreeMenuCopy => "Copy";
    public string TreeMenuPaste => "Paste Here";
    public string TreeMenuDelete => "Delete";
    public string CompareOnTooltip => "Compare Folders (both panes)";
    public string CompareOffTooltip => "Turn Off Folder Compare";
    public string SyncBidirectional => "Two-way Sync";
    public string SwapPanesTooltip => "Swap Left and Right Panes (Ctrl+U)";
    public string SyncPathRightTooltip => "Send left pane path to the right (Ctrl+Shift+→)";
    public string TerminalTooltip => "Open Terminal (active pane folder, Ctrl+`)";
    public string PatternSelectTitle => "Select by Pattern";
    public string PatternHint => "e.g. *.jpg, IMG_*.png";
    public string PatternDeselect => "Deselect";
    public string PatternAdd => "Add to Selection";
    public string NetworkTooltip => "Connect to Network Drive";
    public string NetworkConnectSftp => "Connect to SFTP Server…";
    public string NetworkRemoveProfile => "Remove Saved Server";
    public string ConnectTitle => "Connect to Server";
    public string NetworkConnectFtp => "Connect to FTP Server…";
    public string NetworkConnectWebDav => "Connect to WebDAV Server…";
    public string ConnectPath => "Start folder (optional)";
    public string ConnectPathHint => "e.g. /remote.php/dav/files/me/";
    public string ConnectProtocol => "Protocol";
    public string ConnectAnonymous => "Connect anonymously";
    public string ConnectHost => "Server";
    public string ConnectHostHint => "e.g. example.com";
    public string ConnectPort => "Port";
    public string ConnectUser => "User name";
    public string ConnectPassword => "Password";
    public string ConnectSave => "Save this server (the password is never saved)";
    public string ConnectButton => "Connect";
    public string ConnectBusy => "Connecting…";
    public string NetworkDisconnect(string server) => $"Disconnect: {server}";
    public string SyncPathLeftTooltip => "Send right pane path to the left (Ctrl+Shift+←)";
    public string SinglePaneTooltip => "Single Pane Mode (Ctrl+\\)";
    public string DualPaneTooltip => "Dual Pane Mode (Ctrl+\\)";
    public string DockedPaneTooltip(string path) => $"Collapsed pane: {path}\nClick to switch to it, drop files to copy/move";
    public string SyncUseLeft => "Use Left File";
    public string SyncUseRight => "Use Right File";
    public string SyncDiffToRight(int count) => $"Copy {count} differences →";
    public string SyncDiffToLeft(int count) => $"← Copy {count} differences";
    public string SyncConflictTitle(string name) => $"{name} — which one to keep?";
    public string SyncConflictLeft(string size, string modified) => $"Left: {size} · {modified}";
    public string SyncConflictRight(string size, string modified) => $"Right: {size} · {modified}";
    public string ContextReveal => "Show in File Manager";

    public string FileErrorText(FileError error) => error switch
    {
        FileError.NameEmpty => "Enter a name",
        FileError.NameReserved e => $"That name can't be used: {e.Name}",
        FileError.NameInvalidChar e => $"The name contains a character that isn't allowed: {e.Char}",
        FileError.IntoItself e => $"Can't copy or move a folder into itself: {e.Name}",
        FileError.DifferentTypes e => $"A file and a folder can't overwrite each other: {e.Name}",
        FileError.RootNotAllowed => "The root can't be copied or moved",
        FileError.AlreadyExists e => $"That name already exists: {e.Name}",
        FileError.NotFound e => $"Not found: {e.Name}",
        FileError.AccessDenied e => $"Access denied, or in use elsewhere: {e.Name}",
        FileError.TrashFailed e => $"Couldn't move to the trash: {e.Name}" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.TargetFolderMissing => "The target folder doesn't exist. Check the path",
        FileError.EditorNotFound e => $"Editor not found: {e.Path}. Choose it again in Settings",
        FileError.EditorTooLarge e => $"Files over 5 MB can't be edited. Open it with F3 View: {e.Name}",
        FileError.RemoteNotConnected e => $"Not connected to {e.Server}. Connect from the network menu",
        FileError.RemoteAuthFailed => "The user name or password was rejected",
        FileError.RemoteConnectFailed e => "Couldn't connect to the server" + (string.IsNullOrEmpty(e.Detail) ? "" : $" ({e.Detail})"),
        FileError.RemoteHostKeyChanged e => $"The security key of {e.HostPort} differs from the one seen before. The server may have changed, or someone may be intercepting the connection",
        FileError.RemoteUnsupported => "This isn't available for network items yet",
        FileError.TerminalNotFound => "Couldn't find a terminal program",
        FileError.EditorLaunchFailed e => "Couldn't start the editor" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.Other e => e.Detail ?? "Unknown error",
        _ => "Unknown error",
    };
}
