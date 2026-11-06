"""로컬 WebDAV 통합 테스트용 최소 서버.

wsgidav CLI는 사용자명/비밀번호 인증을 설정 파일 없이는 못 켜서, 이 작은
스크립트로 WsgiDAVApp을 직접 구성하고 cheroot WSGI 서버로 띄운다.

사용: python3 webdav_test_server.py <host> <port> <root_dir> <username> <password>
필요 패키지: pip3 install --user wsgidav cheroot
"""

import sys

from cheroot import wsgi
from wsgidav.wsgidav_app import WsgiDAVApp


def main(host: str, port: int, root: str, username: str, password: str) -> None:
    config = {
        "host": host,
        "port": port,
        "provider_mapping": {"/": root},
        "simple_dc": {"user_mapping": {"/": {username: {"password": password}}}},
        "http_authenticator": {
            "domain_controller": None,
            "accept_basic": True,
            "accept_digest": False,
            "default_to_digest": False,
        },
        "verbose": 0,
    }
    app = WsgiDAVApp(config)
    server = wsgi.Server((host, port), app)
    print("READY", flush=True)
    server.start()


if __name__ == "__main__":
    _host, _port, _root, _username, _password = sys.argv[1:6]
    main(_host, int(_port), _root, _username, _password)
