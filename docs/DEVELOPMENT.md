# PetPresence 개발 워크플로

## 브랜치 정책

- `main`: 안정 브랜치입니다. 빌드 가능하고 검증된 상태만 유지합니다.
- `dev`: Codex 기본 작업 브랜치입니다. 일반 구현, 문서, CI 변경은 먼저 `dev`에서 진행합니다.
- `feature/*`: 선택 사항입니다. 큰 기능이나 실험을 분리해야 할 때만 사용합니다.

규칙:

- 절대 force push 하지 않습니다.
- `main`에는 로컬 검증과 CI가 모두 통과한 변경만 병합합니다.
- GitHub remote는 `https://github.com/kelvin926/PetPresence.git` 입니다.

## 프라이버시 규칙

PetPresence는 presence sharing 앱이므로 기본값은 항상 최소 수집/최소 공유입니다.

- 화면 캡처 금지.
- 키로깅 금지.
- global keyboard/mouse hook 금지.
- 브라우저 히스토리 접근 금지.
- 문서 본문 추출 금지.
- raw URL 수집 금지.
- 다른 프로세스에 코드 주입 금지.
- 입력 주입 금지.
- 숨김 감시 동작 금지.
- raw process name과 raw window title은 데스크톱 앱 로컬 분류에서만 사용합니다.
- 서버/공유 DTO에는 raw process name 또는 raw window title을 추가하지 않습니다.
- 서버로 보내는 presence payload는 canonical status, canonical status text, animation key, confidence, timestamp, 친구 라우팅에 필요한 identity 필드로 제한합니다.
- `scripts/verify_stage.py`의 프라이버시/unsafe API 검사는 약화하지 않습니다.
- 빌드를 통과시키기 위해 프라이버시 테스트를 삭제하지 않습니다.

## 테스트 전략

`tests/PetPresence.Tests`는 xUnit 테스트 프로젝트입니다. 기존 console harness가 아니라 `dotnet test`가 실제 regression test를 실행합니다.

## 로컬 검증 명령

Windows-capable .NET SDK 환경에서 다음을 실행합니다.

```bash
dotnet --info
dotnet restore
dotnet build -c Debug
dotnet test -c Debug --no-build
```

repo-native verifier도 항상 실행합니다.

```bash
python3 scripts/verify_stage.py v0
python3 scripts/verify_stage.py v1
python3 scripts/verify_stage.py v2
python3 scripts/verify_stage.py v3
python3 scripts/verify_stage.py v4
```

`python3`이 없으면 `python`으로 동일하게 실행합니다.

## CI 기대 동작

GitHub Actions는 다음 이벤트에서 실행됩니다.

- `main` push
- `dev` push
- `main` 또는 `dev` 대상 pull request

CI는 `windows-latest`에서 .NET 8 SDK와 Python을 설치하고 다음을 실행합니다.

```bash
dotnet restore
dotnet build -c Debug --no-restore
dotnet test -c Debug --no-build
python scripts/verify_stage.py v0
python scripts/verify_stage.py v1
python scripts/verify_stage.py v2
python scripts/verify_stage.py v3
python scripts/verify_stage.py v4
```

## Codex 작업 규칙

- 작업 시작 전에 항상 현재 브랜치와 상태를 확인합니다.

```bash
git branch --show-current
git status --short
```

- 일반 작업은 `dev`에서 진행합니다.
- secret, token, password, private key, `.env` 파일을 커밋하지 않습니다.
- push 전 민감정보 검색과 프라이버시 경계 검사를 수행합니다.
- 실행한 명령, 결과, 실패 사유를 정확히 보고합니다.
