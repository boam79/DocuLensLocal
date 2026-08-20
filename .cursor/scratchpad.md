# DocuLens Local — Scratchpad

## Background and Motivation

사용자 요청: 폴더 선택 후 **인덱싱**이 가능해야 한다. 원본 PDF는 읽기만 하고, 인덱스는 Velopack `current\`가 아닌 `%LOCALAPPDATA%\DocuLensLocal\userdata`에 둔다.

이번 Executor 슬라이스: **Core 인덱싱 API만**. WPF(인덱싱 버튼)는 다른 에이전트가 담당한다. PDFium 본문 추출·OCR은 stub. 파일명/경로·크기·mtime이 로컬 저장소에 남고, UI가 진행률을 받을 수 있으면 성공.

## Key Challenges and Analysis

- 인덱스를 `AppPaths.UserData`에 두어야 업데이트 시 데이터가 지워지지 않는다.
- 원본 PDF를 수정하면 안 된다 (FileAccess.Read, 타임스탬프 유지).
- UI는 `IProgress`/`Start` + `CancellationToken`이면 충분하다.
- 테스트에서 실제 `%LOCALAPPDATA%`를 건드리지 않도록 UserData 경로를 주입한다.

## High-level Task Breakdown

### Task A — Core IndexingService (이번 슬라이스)

- 성공 기준: `dotnet test` 통과. UI가 `new IndexingService().Start(folder, progress, ct)` 호출 가능.
- `*.pdf` 재귀·대소문자 무시. SQLite에 path/size/mtime 기록. 진행률 보고. 추출/OCR TODO stub.

## Project Status Board

- [x] 솔루션 골격·폴더 선택 설정 경로
- [ ] Core IndexingService — 구현·`dotnet test` 12/12 통과. 사용자 확인 대기
- [ ] WPF 인덱싱 버튼 — UI가 `IndexingService.Start`를 호출함. 사용자 확인 대기 (완료 표시 금지)
- [x] v0.1.2 패키징·GitHub Releases 업로드 (인덱싱 버튼 포함). Planner 완료 표시는 사용자 확인 후
- [ ] 인덱싱 완료 후 메인 검색 화면 — 구현됨. 사용자 수동 확인 대기 (완료 표시 금지)
- [x] v0.1.3 패키징·GitHub Releases 업로드 (인덱싱 완료 후 검색 화면). Planner 완료 표시는 사용자 확인 후
- [ ] 검색 UI 현대화 + 정보 탭(버전/히스토리/업데이트) — 구현됨. 사용자 수동 확인 대기 (완료 표시 금지)
- [x] v0.1.4 패키징·GitHub Releases 업로드 (정보 탭 목업 3 스타일). Planner 완료 표시는 사용자 확인 후
- [ ] 자연어 파일명 검색(버스 광고 찾아줘) — 구현·테스트 통과. 사용자 확인 대기 (완료 표시 금지)
- [ ] 본문 추출·OCR·근거 검색 — 구현·테스트 45 통과. 사용자 Mac/`dotnet run` 확인 대기 (완료 표시 금지)
- [ ] macOS 개발·실행 — Avalonia `net10.0`, Mac 설치 없음. `dotnet run`으로 테스트. 사용자 확인 대기 (완료 표시 금지)

## Executor's Feedback or Assistance Requests

- 2026-08-18: [Core PDF 인덱싱](eb37c726-b43d-42f2-91bc-ac502f16e118) — `IndexingService` 구현 완료. WPF는 수정하지 않음. 사용자 확인 대기.
- UI 호출: `var svc = new IndexingService(); await svc.Start(folderPath, progress, ct);`
- 인덱스 파일: `AppPaths.IndexDatabase` = `%LOCALAPPDATA%\DocuLensLocal\userdata\index.db`
- PDFium/OCR는 TODO stub. 파일명·경로·크기·mtime은 저장되고 `Status = "indexed"`.
- 2026-08-18 (WPF): [WPF 인덱싱 버튼](e2cc63d9-6729-47b8-b1ef-2d47d4f0b6df)이 `MainWindow`에 **인덱싱**을 연결함. 폴더 선택만으로는 시작하지 않음. Core `IndexingService.Start` 호출 확인됨. 커밋하지 않음.
- Planner/사용자에게 수동 확인 요청: 폴더 없는 상태에서 버튼이 꺼져 있는지, 폴더 선택 후 **인덱싱**을 눌러 건수·현재 파일이 보이는지. 확인되면 이 두 항목을 완료로 표시해 주세요.
- 2026-08-18 (패키징 Executor): **v0.1.2** 팩·업로드 완료. Setup.exe를 `-Wait`로 실행하지 않음.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.2/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.1 대비: splash + 폴더 선택에 **인덱싱 버튼 + Core IndexingService** 추가. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - 버전 bump 커밋 `a738e66` push됨. Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (검색 화면 Executor): 인덱싱 `Start` 성공 후 최초실행 폴더 UI를 닫고 **문서 검색** 화면으로 전환. 설치본 v0.1.2에는 없음. 커밋·팩·업로드 하지 않음.
  - 시작 시 `index.db`에 문서가 있거나 `settings.IndexCompleted`이면 검색 화면. 폴더 미선택이면 기존 PRD 최초실행 문구 유지.
  - 0건 인덱싱도 검색 화면(빈 상태). 폴더 선택만으로는 인덱싱 시작 안 함. 원본 PDF 수정 없음.
  - `dotnet test` 18/18, App 빌드 성공.
  - 수동 확인용 exe: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` (설치된 0.1.2를 닫고 실행).
  - Planner/사용자에게 확인 요청: 인덱싱 완료 시 자동으로 검색 화면인지, 재실행 시 검색 화면인지, 폴더 없는 최초실행은 그대로인지.
- 2026-08-18 (릴리스 Executor): **v0.1.3 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `27285cf` push됨: Open filename search after indexing so first-run is not the end, and bump installer to 0.1.3.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.3/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.2 대비: 인덱싱 완료 후 최초실행에 머물지 않고 **파일명 검색 메인 화면**으로 전환. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (UI Executor): 검색 화면을 라이트 모던(둥근 카드·틸 액센트·상단 탭)으로 다듬고 **정보** 탭을 추가함. 커밋·팩·업로드하지 않음.
  - 목업 3장 생성: 라이트 모던 / 사이드바형 / 정보 탭. 구현은 라이트 모던(상단 검색|정보) 방향.
  - 정보 탭: 제품명 + 어셈블리 버전(현재 0.1.3), 하드코드 히스토리 0.1.0–0.1.3, **업데이트** 버튼(Velopack `GithubSource` + `UpdateManager`). 없으면 「최신 버전입니다.」. Debug exe는 설치본이 아니라 적용하지 않음(성공으로 위장하지 않음).
  - 동작 유지: 폴더 선택만으로 인덱싱 안 함, 원본 PDF 읽기 전용, `userdata\index.db`, 파일명 검색.
  - `dotnet test` 27/27, App 빌드 성공.
  - 수동 확인용 exe: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe`
  - Planner/사용자에게 확인 요청: 검색·정보 탭, 버전 표시, 업데이트 버튼 메시지, 더 손볼 UI 방향(라이트/사이드바).
- 2026-08-18 (버전 bump Executor): 설치/앱 버전 선언을 **0.1.3 → 0.1.4**로 올리고 이력에 「정보 탭·모던 UI」를 맨 앞에 추가함. 커밋·팩·업로드하지 않음.
- 2026-08-18 (정보 탭 스타일 Executor): 목업 이미지 3에 맞춰 정보 탭만 다시 스타일함. 버전 선언·ReleaseHistory는 수정하지 않음. 커밋·팩하지 않음.
  - 버전 뱃지: 민트칩+진한 글자 → 틸 필 알약 + 흰 글자(어셈블리 버전).
  - 히스토리: 작은 점 목록 → 세로 타임라인(최신 실원, 이전 빈원, 연결선, 회색 버전 칩).
  - 업데이트: 왼쪽 작은 버튼 → 카드 전체 너비 틸 버튼. 캡션 가운데.
  - 상단 탭을 왼쪽 정렬. 검색 탭 동작(폴더/검색/다시 인덱싱) 유지, 같은 라이트 카드·틸 크롬.
  - App 빌드 성공, `dotnet test` 27/27.
  - 수동 확인: `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` → **정보** 탭이 목업과 가까운지.
- 2026-08-18 (릴리스 Executor): **v0.1.4 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `970672a` push됨: Restyle the Info tab to match design mockup 3 and bump the installer to 0.1.4.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.4/DocuLensLocal-win-Setup.exe (HEAD 200)
  - 0.1.3 대비: 정보 탭 타임라인 + 틸 버전 알약(흰 글자) + 전체너비 업데이트 버튼 / 모던 크롬. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청.
- 2026-08-18 (검색 버그 Executor): PRD P0/P1 스토리를 코드·index.db·테스트로 실행. 화면 버그 「버스 광고 찾아줘」→ 조건에 맞는 파일이 없습니다 를 수정함. 커밋·팩은 이어서 0.1.5로 진행.
  - 원인: `SearchByFileName`이 path LIKE 전체 문장만 봄. 「찾아줘」가 파일명에 없어서 0건.
  - 실제 index.db(읽기 전용): 276건. `버스`/`광고` 파일명 13건(예: `버스티브이.pdf`, `…광고 계약서.pdf`). 둘 다 들어 있는 파일명은 없음 → AND 0건이면 OR로 보여야 함.
  - 수정: 공백 분리 + 찾아줘/해줘/관련/문서 등 제거. 의미 토큰 AND 우선, 비면 OR. 본문은 PC 밖으로 안 보냄. MainWindow는 기존 `SearchByFileName` + 빈 검색=전체.
  - 실행한 스토리: 최초실행 폴더 문구(XAML PRD 문구 일치), 인덱싱 원본 비변경(기존 테스트), 파일명 키워드(mou 테스트 유지), NL 파일명 검색(신규 테스트), 정보 탭 버전(0.1.5).
  - 남은 P0: PDFium 본문 추출·OCR·미리보기·근거 문장. 본문 검색은 stub. 파일명 NL만 이번 슬라이스.
  - `dotnet test` 34/34, App 빌드 성공.
  - 수동 확인: Debug `src\DocuLensLocal.App\bin\Debug\net10.0-windows\DocuLensLocal.exe` 검색창에 `버스 광고 찾아줘`. 설치본은 0.1.5 업데이트 후.
- 2026-08-18 (릴리스 Executor): **v0.1.5 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - 커밋 `77472ef` push됨: Match Korean NL filename queries and bump installer to 0.1.5.
  - 로컬: `C:\Users\tttt\DocuLensLocal\artifacts\Releases\DocuLensLocal-win-Setup.exe`
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.5/DocuLensLocal-win-Setup.exe (HEAD 302, follow 200, 72880377 bytes)
  - 0.1.4 대비: 「버스 광고 찾아줘」가 파일명 키워드(버스/광고)로 검색됨. 설치 페이로드만 업로드(PDF/인덱스 없음).
  - Planner에게 설치본 수동 확인 요청. 완료 표시는 사용자 확인 후.
- 2026-08-19 (macOS Executor): WPF(`net10.0-windows`)를 Avalonia 11.3.20(`net10.0`)으로 바꿔 Windows·Mac에서 같은 앱을 빌드·실행하게 함.
  - `dotnet test` 37/37 (Linux 클라우드). `dotnet build` App 0 warning.
  - 인덱스 경로: Windows `%LOCALAPPDATA%\DocuLensLocal`, Mac `~/Library/Application Support/DocuLensLocal`.
  - pack.ps1은 PATH의 `dotnet` + OS별 RID(`win-x64`/`osx-arm64`/`osx-x64`). `scripts/pack.sh` 추가.
  - Mac 설치 pkg는 `assets/app.icns` + 실제 Mac이 필요. 개발 테스트는 `dotnet run --project src/DocuLensLocal.App`.
  - 버전 선언 0.1.6. GitHub Releases 업로드는 하지 않음.
  - Planner/사용자에게 확인 요청: Mac에서 SDK 10 설치 후 `dotnet test`와 앱 실행, 폴더 선택·인덱싱·검색.
- 2026-08-19 (사용자 정정): Mac 설치(.pkg)는 하지 않는다. Mac은 개발·실제 앱 테스트만. pack.sh·osx RID 패키징 제거. `pack.ps1`은 Windows Setup.exe만. README도 설치 대상은 Windows, Mac은 `dotnet run`.
- 2026-08-19 (본문·OCR Executor): 파일명만 저장하던 인덱스를 **본문+OCR**로 바꿈.
  - PdfPig 디지털 텍스트. 글자 거의 없고 이미지가 있으면 Tesseract CLI(kor+eng) OCR. 원문은 서버로 안 보냄, 원본 mtime 유지.
  - 검색: 파일명 또는 본문. 결과 칩(파일명/본문/OCR) + 근거 스니펫. 더블클릭으로 PDF 열기.
  - `dotnet test` 45/45.
  - 사용자 확인: 본문에만 있는 단어로 검색되는지, 스캔 PDF는 tesseract 설치 후인지.

- 2026-08-20 (릴리스 Executor): **v0.1.7 uploaded.** Setup.exe를 `-Wait`로 실행하지 않음.
  - main `daa2a33` + tag `v0.1.7` push.
  - GitHub: https://github.com/boam79/DocuLensLocal/releases/download/v0.1.7/DocuLensLocal-win-Setup.exe
  - 0.1.5 대비: Avalonia(Mac에서 `dotnet run`), 본문 추출, 로컬 OCR, 근거 문장. 설치 페이로드만 업로드.
  - Windows 설치본은 정보 탭 **업데이트**로 0.1.7을 받을 수 있음.

- 2026-08-20 (UI 점검): 화면에서 276건은 보이는데 검색 버튼·목록·하단 버튼 글자가 없음.
  - 원인: Avalonia `ContentPresenter`에 `Content="{TemplateBinding Content}"`가 없음.
  - 수정: 0.1.8. 인덱스는 살아 있음. 업데이트 후 파일명/본문이 목록에 보여야 함.
- 2026-08-20 (0.1.10 Executor): 스크린샷 「본문 0건 / OCR 엔진 없음 / 부대」.
  - 자동 재인덱스: `IndexBackfillPolicy` — 문서>0 && 본문==0 && 저장 폴더 존재 시 시작 직후 `IndexingService.Start`.
  - OCR: charlesw Tesseract 5.2.0 (`tesseract50.dll`) + tessdata_fast kor/eng를 설치본에 포함. CLI는 보조.
  - `dotnet test` 63/63. 사용자 확인 전 완료 표시 금지.

## Current Status / Progress Tracking

- 모드: **Executor** (v0.1.10 구현 — 사용자 확인 전 완료 표시 금지)
- 브랜치: `cursor/pdf-body-ocr-search-3495`
- 화면 증상(사용자 스크린샷): `인덱싱 완료 · 276건 · 본문 0건 · OCR 엔진 없음`, `부대` 검색은 파일명만 봄.
- 원인: 예전 파일명-only `index.db`를 그대로 씀. Windows에 Tesseract가 PATH에 없어 OCR 배지까지 꺼짐.
- 이번 슬라이스: 설치본에 Tesseract native + tessdata(kor/eng) 포함. 본문 0건이면 저장된 폴더를 자동 재인덱스. 버전 0.1.10.
- `dotnet test` 63/63 (Linux 클라우드).
- 다음: 팩·GitHub Release v0.1.10 업로드 후 사용자에게 정보 탭 **업데이트** 요청.

---

## Background and Motivation (2026-08-19 추가)

사용자 요청: Windows에서 시작한 프로젝트를 **Mac에서도 개발·프로그램 테스트**할 수 있게 한다. 저장소: https://github.com/boam79/DocuLensLocal.git

원인: `DocuLensLocal.App`이 `net10.0-windows` + WPF라 macOS/Linux에서 빌드·실행이 불가하다. `scripts/pack.ps1`도 `C:\Program Files\dotnet\dotnet.exe`에 고정되어 있다.

## Key Challenges and Analysis (2026-08-19 추가)

- WPF는 Windows 전용. Mac에서 GUI를 돌리려면 Avalonia 같은 크로스플랫폼 UI가 필요하다.
- Core/Worker/Tests는 이미 `net10.0`이라 경로만 OS API(`LocalApplicationData`)를 쓰면 Mac·Linux에서도 동작한다.
- Velopack Mac 패키징은 **Mac에서만** 가능. 개발·테스트는 `dotnet run`으로 충분하다.
- 인덱스는 Windows `%LOCALAPPDATA%\DocuLensLocal`, Mac `~/Library/Application Support/DocuLensLocal`.

## High-level Task Breakdown (2026-08-19)

## Background and Motivation (2026-08-19 본문·OCR)

사용자: 프로젝트가 부족하다. 인덱싱/OCR이 제대로인지 모르겠고, 파일 검색기와 차이가 없다.

원인: 인덱스가 경로·크기·mtime만 저장. 본문 추출·OCR은 stub. 검색은 파일명 LIKE뿐.

차별점(이번에 구현):
- 디지털 PDF 본문을 이 PC에서만 추출 (PdfPig)
- 글자 없는 스캔 페이지는 로컬 Tesseract OCR
- 파일명+본문 검색, 히트 시 근거 문장(스니펫)
- 원문은 서버로 안 보냄, 원본 PDF 수정 없음

## High-level Task Breakdown (2026-08-19 본문·OCR)

### Task C — 본문 인덱스·OCR·근거 검색

- 성공 기준: 본문에만 있는 단어로 검색됨. 스니펫 표시. OCR 엔진이 있으면 이미지 페이지 글자를 넣음. `dotnet test` 통과. 원본 mtime 유지.

- 성공 기준: `dotnet test` 통과. App이 `net10.0`(WPF 아님). Mac에서 `dotnet run`으로 GUI 테스트. Windows 설치 팩만 `pack.ps1`. Mac 설치 파일은 만들지 않음.

## Lessons

- 기능 검증은 배포 URL에서 수행 (웹). 이 작업은 로컬 .NET 라이브러리라 `dotnet test`가 검증이다.
- `dotnet` PATH가 비면 `C:\Program Files\dotnet\dotnet.exe`를 쓴다.
- 인덱스를 Velopack `current\`에 두면 업데이트 때 사라진다. UserData만 사용.
- Microsoft.Data.Sqlite 기본 연결 풀은 Windows에서 `index.db`를 잠근다. `Pooling=false` + `ClearAllPools()`가 필요하다.
- 인덱싱 `완료`는 최초실행 화면의 종착점이 아니다. `Start` 성공(`IsCompleted`)이면 검색 화면으로 넘겨야 한다. 0건도 동일. 재시작은 `index.db` 문서 수 또는 `AppSettings.IndexCompleted`로 판단한다.
- Velopack `UpdateManager.IsInstalled`가 false인 Debug exe에서는 업데이트를 적용하지 말고, 「최신 버전입니다.」로 위장하지 않는다. 공개 저장소 확인은 `GithubSource(repoUrl, accessToken: null, prerelease: false)`.
- 한국어 NL 파일명 검색은 문장 전체 LIKE가 아니라 토큰(버스/광고) AND→OR. 실제 276건 인덱스에는 버스+광고 동시 파일명이 없어 OR 폴백이 필수다.
- WPF(`net10.0-windows`)는 Mac에서 빌드·실행이 안 된다. 데스크톱 UI는 Avalonia + `net10.0`으로 둔다. Mac 경로는 `LocalApplicationData` → `~/Library/Application Support/DocuLensLocal`.
- 사용자는 Mac 설치본을 원하지 않는다. Mac은 `dotnet run`으로 개발·기능 테스트만. `pack.ps1`은 Windows Setup.exe만 만든다.
- 파일명만 인덱싱하면 탐색기와 차별이 없다. 디지털 PDF는 PdfPig 본문, 스캔은 로컬 Tesseract. 검색 결과는 근거 스니펫이 있어야 한다.
- Windows 사용자는 Tesseract를 따로 설치하지 않는다. 설치본에 `x64/tesseract50.dll` + `tessdata/eng|kor.traineddata`(tessdata_fast)를 넣고, PATH CLI는 보조만 쓴다.
- 예전 인덱스(본문 0건)는 안내 문구만으로 해결되지 않는다. 저장된 `IndexFolder`가 있으면 시작 시 자동 재인덱싱해야 한다.
- charlesw Tesseract 5.2.0 native 파일 이름은 `tesseract41.dll`이 아니라 `tesseract50.dll`이다.

