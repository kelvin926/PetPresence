# PetPresence Windows 런타임 스모크 테스트 결과

# 실제 Windows desktop smoke test - 2026-05-07

## 실행 정보

- 날짜: 2026-05-07 17:04:38 KST
- Repository path: `E:\PetPresence`
- Branch: `dev`
- 테스트 대상 commit: `d64604d55a7257f5d6296dbd27bddbd23cc65de0`
- 결과 요약: **부분 성공 / Desktop two-client presence 버그 발견**

## 환경

- 실행 세션: Windows PowerShell 5.1.26100.8115, 실제 Windows desktop session
- Windows: `Microsoft Windows NT 10.0.26200.0`
- .NET SDK: `8.0.420`
- Python: `Python 3.13.2`
- 참고: `dotnet`은 최초 PATH에 없어서 `winget install Microsoft.DotNet.SDK.8`로 .NET 8 SDK를 설치했고, Machine/User PATH를 새로 읽은 PowerShell에서 이후 검증을 수행했습니다.

## Build/test/verifier 결과

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| `git status --short` | 성공 | clean |
| `git branch --show-current` | 성공 | `dev` |
| `dotnet restore` | 성공 | 모든 프로젝트 복원 |
| `dotnet build -c Debug` | 성공 | 경고 0, 오류 0 |
| `dotnet test -c Debug --no-build` | 성공 | 21 passed, 0 failed, 0 skipped |
| `python scripts/verify_stage.py v0` | 성공 | `OK: v0 verification passed` |
| `python scripts/verify_stage.py v1` | 성공 | `OK: v1 verification passed` |
| `python scripts/verify_stage.py v2` | 성공 | `OK: v2 verification passed` |
| `python scripts/verify_stage.py v3` | 성공 | `OK: v3 verification passed` |
| `python scripts/verify_stage.py v4` | 성공 | `OK: v4 verification passed` |
| `PowerShell -ExecutionPolicy Bypass -File .\scripts\smoke-windows.ps1` | 성공 | 설치 직후 기존 PATH에서는 `dotnet restore`를 찾지 못해 1회 실패. 새 PATH를 읽은 PowerShell에서 restore/build/test/verifier 전체 통과 |

## Server startup 결과

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| server startup | 성공 | `dotnet run --project src/PetPresence.Server -c Debug` |
| listening URL | 성공 | `http://127.0.0.1:6500` |
| health check | 성공 | `/health` 응답: `{"service":"PetPresence.Server","version":"v4"}` |
| process remains running | 성공 | runtime smoke 중 서버 프로세스 유지 |

참고: 최초 임의 포트 `5137`은 Windows excluded port range `5041-5140`에 포함되어 SocketException 10013으로 바인딩 실패했습니다. 제품 코드 수정 없이 `6500`으로 재시도했습니다.

## Standalone desktop overlay 결과

| 항목 | 결과 | 실제 관찰 |
| --- | --- | --- |
| transparent overlay appears | 성공 | `PetPresence Overlay` WPF window visible |
| tray icon appears | 성공 | Windows shell UI Automation에서 `PetPresence` tray ListItem/Button 확인 |
| own pet appears | 성공 | overlay UI Automation text: `나`, status text, animation key 확인 |
| app does not steal focus | 성공 | foreground window owner가 `PetPresence.Desktop.exe`가 아님 |
| normal mode click-through | 성공 | overlay extended style `WS_EX_TRANSPARENT=True`, `WS_EX_NOACTIVATE=True`, `WS_EX_LAYERED=True` |
| edit mode allows dragging | 미수행 | tray menu 클릭과 drag는 금지 규칙의 입력 주입 없이 수행할 수 없어 수동 미수행 |
| exiting app works cleanly | 부분 실패 | overlay에 `WM_CLOSE`를 보내면 visible overlay는 사라졌지만 프로세스는 남음. tray `Exit` 메뉴는 입력 주입 없이 누를 수 없어 미수행 |

## Activity classification 결과

금지 규칙 때문에 화면 캡처, 키보드/마우스 입력 주입 없이 앱 실행 및 window activation만 사용했고, 상태 관찰은 UI Automation으로 overlay의 자체 텍스트만 읽었습니다.

| 케이스 | 기대 | 실제 status text | 실제 animation | 대략 지연 | flicker/메모 |
| --- | --- | --- | --- | --- | --- |
| VS Code foreground | `코딩 중...` / `typing` | `코딩 중...` | `typing` | 약 5.8초 | 기대 상태 도달 후 자동화 셸/다른 창으로 focus가 이동하면서 `상태 확인 중...`으로 되돌아간 샘플 있음 |
| Word foreground | `문서 작성 중...` / `typing` | `문서 작성 중...` | `typing` | 약 5.9초 | 약 16초 이상 안정적으로 유지, 이후 focus 이탈 시 `상태 확인 중...` |
| Edge YouTube-title foreground | `영상 보는 중...` / `watching` | `영상 보는 중...` | `watching` | 약 9.6초 | local temp HTML title을 InPrivate Edge에서 사용. 유지 중 flicker 없음 |
| Edge search-title foreground | `웹서칭 중...` 또는 `웹 보는 중...` / `browsing` | `웹서칭 중...` | `browsing` | 약 5.7초 | 약 18초 유지, 이후 focus 이탈 시 `상태 확인 중...` |
| idle threshold | `자리 비움...` / `away` | `자리 비움...` | `away` | 앱 시작 후 8초 이내 | OS idle이 이미 threshold 초과 상태였고 10초 샘플에서 안정적 |

False positive/negative 메모:

- foreground가 유지된 구간에서는 기대 분류가 모두 한 번 이상 도달했습니다.
- 자동화 셸이 foreground를 다시 가져오면 `Unknown`으로 되돌아가는 샘플이 있어, 순수 수동 관찰보다 focus 안정성이 낮았습니다.
- 브라우저 검증은 browser history를 읽지 않았고, InPrivate Edge에서 `.omx\runtime-smoke` local HTML title만 사용했습니다.

## Friend presence 결과

### Desktop Alice/Bob smoke

| 항목 | 기대 | 실제 결과 |
| --- | --- | --- |
| Alice client 실행 | `PETPRESENCE_USER_ID=alice`로 Alice pet 표시 | 실패. overlay text는 `나`만 표시 |
| Bob client 실행 | `PETPRESENCE_USER_ID=bob`로 Bob pet 표시 | 실패. overlay text는 `나`만 표시 |
| accepted 전 visibility | 서로 friend pet 미표시 | `alice`/`bob` 텍스트 없음. 단, 클라이언트가 둘 다 `local-user`로 동작해서 올바른 사전 조건 아님 |
| friendship request/accept | Alice/Bob friendship accepted | 성공. 아래 REST 명령으로 server store는 accepted 상태 |
| accepted 후 friend pet 표시 | friend pet 표시 | 실패. accepted 후에도 desktop overlay에 `alice`/`bob` friend pet 미표시 |
| friend activity update | 상대 activity 반영 | 미수행/차단. Desktop user id hardcoding 때문에 Alice/Bob 클라이언트 분리가 되지 않음 |
| disconnect/offline | 상대 offline 표시 | Desktop two-client 경로는 미수행/차단. 서버 SignalR probe에서는 offline 전달 확인 |

친구 API에 사용한 명령:

```powershell
$alice = @{ 'X-User-Id' = 'alice' }
$bob = @{ 'X-User-Id' = 'bob' }
Invoke-RestMethod -Uri 'http://127.0.0.1:6500/friends/request' -Headers $alice -Method Post -ContentType 'application/json' -Body '{"friendUserId":"bob"}'
Invoke-RestMethod -Uri 'http://127.0.0.1:6500/friends/accept' -Headers $bob -Method Post -ContentType 'application/json' -Body '{"friendUserId":"alice"}'
Invoke-RestMethod -Uri 'http://127.0.0.1:6500/friends' -Headers $alice
Invoke-RestMethod -Uri 'http://127.0.0.1:6500/friends' -Headers $bob
```

REST 결과:

- request: `requesterId=alice`, `addresseeId=bob`, `status=Pending`
- accept: `requesterId=alice`, `addresseeId=bob`, `status=Accepted`
- Alice friends: `bob`, `Accepted`
- Bob friends: `alice`, `Accepted`

### Server accepted-friend-only routing probe

Desktop의 Alice/Bob 분리 버그와 별도로 `.omx\runtime-smoke\FriendRoutingProbe` 임시 SignalR 클라이언트로 server routing을 검증했습니다. 제품 코드는 수정하지 않았습니다.

| 항목 | 결과 | 메모 |
| --- | --- | --- |
| accepted 전 Carol이 Dave presence 수신 안 함 | 성공 | `before_accept_count=0` |
| accepted 후 Carol이 Dave presence 수신 | 성공 | `after_accept_received=True` |
| canonical DTO 적용 | 성공 | raw text/animation을 보냈지만 수신값은 `코딩 중...` / `typing` |
| Dave disconnect 후 offline 전달 | 성공 | `offline_received=True`, `오프라인...` / `offline` |
| accepted-friend-only routing | 성공 | 서버/Hub 경로에서는 통과 |

## Privacy observations

- 화면 캡처를 사용하지 않았습니다.
- 키로깅을 사용하지 않았습니다.
- global keyboard/mouse hook을 사용하지 않았습니다.
- keyboard/mouse input injection을 사용하지 않았습니다.
- browser history에 접근하지 않았습니다.
- 문서 본문 내용은 읽지 않았습니다.
- 다른 프로세스에 inject하지 않았습니다.
- raw process names/window titles를 서버로 보내지 않았습니다.
- server DTO는 canonical presence만 유지했습니다. SignalR probe에서 raw `StatusText`/`AnimationKey`를 보내도 서버가 `코딩 중...` / `typing`으로 canonicalize하는 것을 확인했습니다.

## Bugs found

### Bug 1: Desktop app ignores `PETPRESENCE_USER_ID`

- 재현 단계:
  1. server를 `http://127.0.0.1:6500`에서 실행합니다.
  2. terminal C에서 `$env:PETPRESENCE_USER_ID="alice"`, `$env:PETPRESENCE_SERVER_URL="http://127.0.0.1:6500"` 설정 후 Desktop을 실행합니다.
  3. terminal D에서 `$env:PETPRESENCE_USER_ID="bob"`, `$env:PETPRESENCE_SERVER_URL="http://127.0.0.1:6500"` 설정 후 Desktop을 실행합니다.
  4. `/friends/request`, `/friends/accept`로 Alice/Bob friendship을 accepted로 만듭니다.
- 기대:
  - Alice/Bob 클라이언트가 서로 다른 user id로 접속하고, accepted 후 friend pet이 표시됩니다.
- 실제:
  - 두 Desktop overlay 모두 display text가 `나`이고, accepted 후에도 `alice`/`bob` friend pet이 표시되지 않습니다.
- 추정 원인:
  - `src/PetPresence.Desktop/App.xaml.cs`가 own pet, `LocalPresenceController`, `PresenceClient` 생성 시 모두 `"local-user"`를 하드코딩합니다. `PETPRESENCE_USER_ID`는 읽지 않습니다.
- 심각도:
  - High. two-client friend presence smoke test를 제품 Desktop 경로에서 통과할 수 없습니다.

### Bug 2: overlay `WM_CLOSE` 후 process가 남음

- 재현 단계:
  1. standalone Desktop을 실행합니다.
  2. `PetPresence Overlay` window에 `WM_CLOSE`를 보냅니다.
- 기대:
  - main overlay close가 앱 종료이거나, 사용자가 회복 가능한 명확한 상태여야 합니다.
- 실제:
  - visible overlay는 사라졌지만 `PetPresence.Desktop.exe` 프로세스는 계속 남았습니다.
- 추정 원인:
  - tray icon/WinForms hidden window가 프로세스를 유지하거나, 종료 경로가 tray `Exit` 메뉴에만 묶여 있습니다.
- 심각도:
  - Low. tray `Exit` 메뉴의 실제 클릭 종료는 입력 주입 금지 때문에 미수행이라 별도 확인이 필요합니다.

## 다음 권장 수정

1. Desktop startup에서 `PETPRESENCE_USER_ID`를 읽어 own pet id, `LocalPresenceController`, `PresenceClient`에 동일하게 전달합니다.
2. 두 Desktop 인스턴스가 동시에 떠도 overlay 식별/표시가 가능한 user display name을 환경 또는 설정에서 분리합니다.
3. tray `Exit`를 입력 주입 없이도 테스트할 수 있는 종료 hook 또는 debug-only smoke endpoint를 검토합니다. 제품 privacy verifier는 유지해야 합니다.
4. accepted friendship 후 기존 friend의 latest presence를 받는 초기 sync가 필요한지 검토합니다. 현재 server는 update/disconnect event만 push합니다.

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
