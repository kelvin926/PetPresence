# PetPresence 개발 워크플로

## 브랜치 정책

- `main`: 안정 브랜치입니다. 빌드 가능하고 검증된 상태만 유지합니다.
- `dev`: Codex 작업 브랜치입니다. 일반 구현, 문서, CI 변경은 먼저 `dev`에서 진행합니다.
- `feature/*`: 선택 사항입니다. 큰 기능이나 실험을 분리해야 할 때만 사용합니다.

## 프라이버시 규칙

PetPresence는 presence sharing 앱이므로 기본값은 항상 최소 수집/최소 공유입니다.

- 화면 캡처 금지.
- 키로깅 금지.
- 브라우저 히스토리 접근 금지.
- 다른 프로세스에 코드 주입 금지.
- 입력 주입 금지.
- 숨김 감시 동작 금지.
- raw process name과 raw window title은 데스크톱 앱 로컬 분류에서만 사용합니다.
- 서버/공유 DTO에는 raw process name 또는 raw window title을 추가하지 않습니다.
- 서버로 보내는 presence payload는 canonical status, status text, animation key, confidence, timestamp, 친구 라우팅에 필요한 identity 필드로 제한합니다.

## 검증 명령

기본 verifier:

```bash
python3 scripts/verify_stage.py v0
python3 scripts/verify_stage.py v1
python3 scripts/verify_stage.py v2
python3 scripts/verify_stage.py v3
python3 scripts/verify_stage.py v4
```

`python3`이 없으면 `python`으로 동일하게 실행합니다.

.NET SDK가 설치된 환경에서는 다음도 실행합니다.

```bash
dotnet --info
dotnet restore
dotnet build -c Debug
dotnet test -c Debug --no-build
```

## GitHub remote

```text
https://github.com/kelvin926/PetPresence.git
```

## Codex 작업 규칙

- 작업 시작 전에 항상 현재 브랜치와 상태를 확인합니다.

```bash
git branch --show-current
git status --short
```

- 일반 작업은 `dev`에서 진행합니다.
- `main`에는 검증 완료 후 병합합니다.
- 절대 force push 하지 않습니다.
- secret, token, password, private key, `.env` 파일을 커밋하지 않습니다.
- push 전 민감정보 검색과 프라이버시 경계 검사를 수행합니다.
- 실행한 명령, 결과, 실패 사유를 정확히 보고합니다.
