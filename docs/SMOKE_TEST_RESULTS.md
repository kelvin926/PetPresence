# PetPresence Windows 런타임 스모크 테스트 결과

## 실행 정보

- 날짜: 2026-05-07 16:20:38 KST
- Repository path: `/mnt/e/PetPresence-normal`
- Branch: `dev`
- 테스트 대상 commit: `dda0bff72f48968b02655a8be65aa302a92c8075`
- 결과 요약: **미수행 / 환경 차단**

## 환경

- `uname -a`: `Linux HYUNSEOFRBOT 6.6.87.2-microsoft-standard-WSL2 #1 SMP PREEMPT_DYNAMIC Thu Jun 5 18:30:46 UTC 2025 x86_64 GNU/Linux`
- `WSL_DISTRO_NAME`: `Ubuntu-24.04-OMX`
- `WSL_INTEROP`: `/run/WSL/6716_interop`
- `DISPLAY`: `:0`
- `WAYLAND_DISPLAY`: `wayland-0`
- Windows host check: `Microsoft Windows [Version 10.0.26200.8246]`
- Windows PowerShell: `5.1.26100.8115`
- Linux `dotnet`: 설치되어 있지 않음 (`dotnet: command not found`)
- Windows `dotnet.exe`: `where dotnet` 실패

## Runtime smoke test 수행 여부

**WPF overlay runtime behavior was not tested because this is not an interactive Windows desktop session.**

현재 세션은 WSL2/Linux shell이며, Windows WPF overlay를 실제 데스크톱에서 띄워 focus/click-through/tray/edit-mode 동작을 사람이 확인할 수 있는 interactive Windows desktop session이 아닙니다. 따라서 overlay 실행, tray 확인, click-through 확인, edit mode dragging 확인, classification 수동 확인, two-client friend presence 수동 확인은 수행하지 않았습니다.

## Build/test 결과

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| `dotnet restore` | 미수행 | 이 환경에 `dotnet`이 설치되어 있지 않음 |
| `dotnet build -c Debug` | 미수행 | `dotnet` 없음 |
| `dotnet test -c Debug --no-build` | 미수행 | `dotnet` 없음 |
| `python3 scripts/verify_stage.py v0` | 성공 | `OK: v0 verification passed` |
| `python3 scripts/verify_stage.py v1` | 성공 | `OK: v1 verification passed` |
| `python3 scripts/verify_stage.py v2` | 성공 | `OK: v2 verification passed` |
| `python3 scripts/verify_stage.py v3` | 성공 | `OK: v3 verification passed` |
| `python3 scripts/verify_stage.py v4` | 성공 | `OK: v4 verification passed` |

참고: 같은 commit의 직전 dev CI는 GitHub Actions `windows-latest`에서 restore/build/test/verifier를 통과했습니다. 하지만 CI 성공은 실제 WPF overlay runtime smoke test를 대체하지 않습니다.

## Overlay 결과

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| overlay 표시 | 미수행 | 실제 Windows desktop session 필요 |
| tray icon 표시 | 미수행 | 실제 Windows desktop session 필요 |
| focus steal 여부 | 미수행 | 실제 Windows desktop session 필요 |
| normal mode click-through | 미수행 | 실제 Windows desktop session 필요 |
| edit mode pet dragging | 미수행 | 실제 Windows desktop session 필요 |
| 앱 종료 clean 여부 | 미수행 | 실제 Windows desktop session 필요 |

## Classification 결과

| 항목 | 결과 | 실제 status text | animation key | 안정성/메모 |
| --- | --- | --- | --- | --- |
| VS Code foreground → `코딩 중...` | 미수행 | N/A | N/A | 실제 Windows desktop session 필요 |
| Word/Notion/Obsidian/Typora → `문서 작성 중...` | 미수행 | N/A | N/A | 실제 Windows desktop session 필요 |
| YouTube title → `영상 보는 중...` | 미수행 | N/A | N/A | 실제 Windows desktop session 필요 |
| browser search page → `웹서칭 중...` | 미수행 | N/A | N/A | 실제 Windows desktop session 필요 |
| idle threshold → `자리 비움...` | 미수행 | N/A | N/A | 실제 Windows desktop session 필요 |

## Friend presence 결과

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| server startup | 미수행 | `dotnet` 없음, interactive Windows runtime 아님 |
| client A `alice` 실행 | 미수행 | 실제 Windows desktop session 필요 |
| client B `bob` 실행 | 미수행 | 실제 Windows desktop session 필요 |
| friendship accept 전 presence 비표시 | 미수행 | 실제 Windows desktop session 필요 |
| friendship accept 후 friend pet 표시 | 미수행 | 실제 Windows desktop session 필요 |
| friend activity 변경 반영 | 미수행 | 실제 Windows desktop session 필요 |
| disconnect/offline status | 미수행 | 실제 Windows desktop session 필요 |

## Privacy observations

- 이 세션에서는 화면 캡처를 수행하지 않았습니다.
- 키로깅 또는 keyboard/mouse hook을 사용하지 않았습니다.
- 입력 주입을 수행하지 않았습니다.
- 브라우저 히스토리에 접근하지 않았습니다.
- 다른 프로세스에 주입하지 않았습니다.
- 런타임 앱을 실행하지 않았으므로 서버 payload를 실제 관찰하지는 못했습니다.
- 정적 privacy verifier는 별도로 실행하여 raw process/window title DTO 노출 및 금지 API 검사를 확인해야 합니다.

## Failures / bugs found

### Environment blocker: interactive Windows desktop session 없음

- 재현 단계:
  1. `/mnt/e/PetPresence-normal`에서 환경 확인 명령 실행.
  2. `uname -a`가 WSL2 Linux kernel을 보고함.
  3. `dotnet --info`와 Windows `where dotnet`이 실패함.
- 기대:
  - 실제 Windows 10/11 desktop session에서 .NET 8 SDK가 설치되어 있고 WPF desktop app 실행 가능.
- 실제:
  - WSL2/Linux shell이며 `dotnet`이 설치되어 있지 않음.
- 추정 원인:
  - 현재 자동화 환경이 Windows GUI 세션이 아니라 WSL 기반 shell임.
- 심각도:
  - Medium for runtime validation. 제품 코드 버그는 아니지만 Windows overlay smoke test를 완료할 수 없음.

## 다음 권장 조치

1. 실제 Windows 10/11 desktop session에서 .NET 8 SDK를 설치한 뒤 `docs/SMOKE_TEST.md` 절차를 실행합니다.
2. `scripts/smoke-windows.ps1`로 restore/build/test/verifier를 먼저 실행합니다.
3. 서버와 데스크톱 앱을 별도 PowerShell terminal에서 실행하고 overlay focus/click-through/edit mode를 수동 확인합니다.
4. `alice`/`bob` 두 클라이언트로 friend presence routing을 확인합니다.
5. 실제 결과를 이 파일에 추가로 기록합니다.
