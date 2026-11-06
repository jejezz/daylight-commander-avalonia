"""로컬 SFTP 통합 테스트용 최소 서버.

pyftpdlib과 달리 asyncssh에는 바로 실행 가능한 CLI가 없어서 이 작은 스크립트로
대신한다. 호스트 키는 매번 메모리에서 새로 만들고(디스크에 남기지 않음),
비밀번호 인증 하나만 지원한다.

`chroot`는 일부러 안 쓴다 — asyncssh의 chroot 구현이 절대경로(예: "/newdir")로
들어온 mkdir/open 요청을 chroot 밖(진짜 파일시스템 루트)으로 잘못 풀어버리는
현상이 있어(dartssh2 클라이언트로 재현됨, 순수 파이썬 클라이언트로는 재현 안
됨 — 클라이언트가 REALPATH를 먼저 묻고 그 결과로 요청하는지 여부 차이로 추정),
macOS에서 "Read-only file system" 에러가 났다. 대신 서버 프로세스가 실제로
접근 가능한 로컬 임시 디렉터리를 그대로 SFTP 루트로 노출한다 — 이 앱이 SFTP
루트를 "/"로 가정하지 않고 그냥 URI의 path를 그대로 쓰므로 실제 서버 동작과도
더 가깝다.

사용: python3 sftp_test_server.py <host> <port> <root_dir> <username> <password>
필요 패키지: pip3 install --user asyncssh
"""

import asyncio
import sys

import asyncssh


class _PasswordAuthServer(asyncssh.SSHServer):
    def __init__(self, username: str, password: str):
        self._username = username
        self._password = password

    def begin_auth(self, username: str) -> bool:
        return True

    def password_auth_supported(self) -> bool:
        return True

    def validate_password(self, username: str, password: str) -> bool:
        return username == self._username and password == self._password


async def _start(host: str, port: int, username: str, password: str) -> None:
    host_key = asyncssh.generate_private_key("ssh-rsa")

    await asyncssh.listen(
        host,
        port,
        server_host_keys=[host_key],
        server_factory=lambda: _PasswordAuthServer(username, password),
        sftp_factory=asyncssh.SFTPServer,
    )
    print("READY", flush=True)
    await asyncio.Event().wait()


if __name__ == "__main__":
    # root_dir는 안 쓰지만(위 설명 참고) 인자 자리는 webdav_test_server.py와
    # 맞춰 호출부 코드를 단순하게 유지한다.
    _host, _port, _root, _username, _password = sys.argv[1:6]
    asyncio.run(_start(_host, int(_port), _username, _password))
