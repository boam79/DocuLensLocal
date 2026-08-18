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
- [ ] PDFium 본문 추출
- [ ] OCR

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

## Current Status / Progress Tracking

- 모드: **Executor** (v0.1.3 업로드 완료, 사용자 수동 확인 대기)
- origin/main: `27285cf`. 릴리스 태그: v0.1.3
- 다음: 사용자가 설치본에서 인덱싱 완료 → 검색 화면을 확인한 뒤 Planner가 완료 표시

## Lessons

- 기능 검증은 배포 URL에서 수행 (웹). 이 작업은 로컬 .NET 라이브러리라 `dotnet test`가 검증이다.
- `dotnet` PATH가 비면 `C:\Program Files\dotnet\dotnet.exe`를 쓴다.
- 인덱스를 Velopack `current\`에 두면 업데이트 때 사라진다. UserData만 사용.
- Microsoft.Data.Sqlite 기본 연결 풀은 Windows에서 `index.db`를 잠근다. `Pooling=false` + `ClearAllPools()`가 필요하다.
- 인덱싱 `완료`는 최초실행 화면의 종착점이 아니다. `Start` 성공(`IsCompleted`)이면 검색 화면으로 넘겨야 한다. 0건도 동일. 재시작은 `index.db` 문서 수 또는 `AppSettings.IndexCompleted`로 판단한다.
