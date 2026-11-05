using System.Globalization;
using DaylightCommander.Domain;
using DaylightCommander.Services;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 이름 충돌 질문 (복사·이동 중). 덮어쓰기·둘 다 유지·건너뛰기·취소를 고르고, "모두 적용"이면 남은 충돌에도 같은 답이 간다.
/// 창을 그냥 닫으면 취소로 답한다.
/// </summary>
public partial class ConflictDialog : DialogBase
{
    public ConflictDialog(ConflictRequest request)
    {
        InitializeComponent();
        Title = Strings.Current.ConflictTitle;
        TitleText.Text = Strings.Current.ConflictTitle;
        FileName.Text = Path.GetFileName(request.Target);
        ExistingText.Text = $"{Strings.Current.ConflictExisting}: {Describe(request.Target)}";
        NewText.Text = $"{Strings.Current.ConflictNew}: {Describe(request.Source)}";
        ApplyToAll.Content = Strings.Current.ConflictApplyAll;
        OverwriteButton.Content = Strings.Current.ConflictOverwrite;
        KeepBothButton.Content = Strings.Current.ConflictKeepBoth;
        SkipButton.Content = Strings.Current.ConflictSkip;
        CancelButton.Content = Strings.Current.CommonCancel;

        OverwriteButton.Click += (_, _) => Decide(ConflictChoice.Overwrite);
        KeepBothButton.Click += (_, _) => Decide(ConflictChoice.KeepBoth);
        SkipButton.Click += (_, _) => Decide(ConflictChoice.Skip);
        CancelButton.Click += (_, _) => Decide(ConflictChoice.Cancel);
    }

    public event Action<ConflictDecision>? Decided;

    void Decide(ConflictChoice choice) =>
        Decided?.Invoke(new ConflictDecision(choice, ApplyToAll.IsChecked == true));

    /// <summary>"12.3 KB · 2026-11-05 10:11" / 폴더는 "폴더 · 수정일". 읽지 못하면 안내 문구.</summary>
    static string Describe(string path)
    {
        try
        {
            var modified = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            return Directory.Exists(path)
                ? Strings.Current.ConflictFolderInfo(modified)
                : $"{BytesFormat.Format(new FileInfo(path).Length)} · {modified}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Strings.Current.ConflictInfoUnavailable;
        }
    }
}
