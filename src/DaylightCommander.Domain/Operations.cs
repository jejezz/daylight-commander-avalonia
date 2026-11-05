namespace DaylightCommander.Domain;

public enum FileOperationKind { Copy, Move, Delete }

/// <summary>대상에 같은 이름이 있을 때의 선택.</summary>
public enum ConflictChoice { Overwrite, Skip, KeepBoth, Cancel }

/// <param name="ApplyToAll">참이면 이 작업의 남은 충돌에 같은 선택을 적용한다.</param>
public sealed record ConflictDecision(ConflictChoice Choice, bool ApplyToAll = false);

/// <summary>충돌이 났을 때 사용자에게 물어 답을 돌려준다. 취소되면 작업도 취소된다.</summary>
public delegate Task<ConflictDecision> ConflictResolver(string source, string target, CancellationToken cancellationToken);

/// <summary>복사·이동은 바이트, 삭제는 항목 수 기준.</summary>
public sealed record OperationProgress(long Done, long Total, string Current)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Done / Total, 0, 1);
}

public sealed record OperationFailure(string Path, FileError Error);

/// <param name="Processed">끝까지 처리한 최상위 항목 수 (건너뛴 것·실패한 것 제외).</param>
public sealed record OperationResult(int Processed, IReadOnlyList<OperationFailure> Failures)
{
    public OperationResult(int processed) : this(processed, []) { }
}
