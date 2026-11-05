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
    public string FontSizeSmall => "Small";
    public string FontSizeNormal => "Normal";
    public string FontSizeLarge => "Large";
    public string SettingsLanguage => "Language";
    public string LanguageSystemShort => "System";

    public string SettingsSectionEditor => "Editor";
    public string EditorTitle => "Text editor (F4)";
    public string EditorBuiltin => "Built-in editor (default)";
    public string EditorExternal => "External program";
    public string EditorNotReady => "The built-in editor isn't ready yet. Pick an external program to open files with F4.";
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
        FileError.EditorLaunchFailed e => "Couldn't start the editor" + (e.Detail is null ? "" : $" ({e.Detail})"),
        FileError.Other e => e.Detail ?? "Unknown error",
        _ => "Unknown error",
    };
}
