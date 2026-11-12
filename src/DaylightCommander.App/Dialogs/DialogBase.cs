using Avalonia.Controls;

namespace DaylightCommander.App.Dialogs;

/// <summary>
/// 짧은 모달 질문 창의 공통 바탕 (복사 대상·삭제 확인·이름 입력·이름 충돌). 내용 크기에 맞춰 열리고 크기를 바꿀 수 없다.
/// 사용자가 창을 닫으면(X, Esc) <see cref="Dismissed"/>가 불리고, 상태가 바뀌어서 프로그램이 닫으면 불리지 않는다.
/// </summary>
public abstract class DialogBase : Window
{
    bool _closedByState;

    protected DialogBase()
    {
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        ShowInTaskbar = false;
        // 창 크롬 정책 A(인-윈도 모달): OS 타이틀바 없이 앱이 둥근 테두리를 직접 그린다 (App.axaml의 Window.dialog 템플릿).
        // 제목은 본문 첫 줄이 맡고, 닫기는 Esc 또는 단추로 한다.
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Avalonia.Media.Brushes.Transparent;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Classes.Add("dialog");
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape) { OnEscape(); e.Handled = true; }
        };
    }

    /// <summary>Esc를 눌렀을 때. 기본은 사용자가 닫은 것으로 본다. 닫으면 안 되는 진행 창은 덮어쓴다.</summary>
    protected virtual void OnEscape() => Close();

    /// <summary>사용자가 확정하지 않고 창을 닫았다.</summary>
    public event Action? Dismissed;

    /// <summary>상태가 바뀌어서 닫는다 (<see cref="Dismissed"/>를 부르지 않는다).</summary>
    public void CloseFromState()
    {
        _closedByState = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (!_closedByState) Dismissed?.Invoke();
    }
}
