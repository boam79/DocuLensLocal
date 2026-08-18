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
- [ ] PDFium 본문 추출
- [ ] OCR

## Executor's Feedback or Assistance Requests

- 2026-08-18: [Core PDF 인덱싱](eb37c726-b43d-42f2-91bc-ac502f16e118) — `IndexingService` 구현 완료. WPF는 수정하지 않음. 사용자 확인 대기.
- UI 호출: `var svc = new IndexingService(); await svc.Start(folderPath, progress, ct);`
- 인덱스 파일: `AppPaths.IndexDatabase` = `%LOCALAPPDATA%\DocuLensLocal\userdata\index.db`
- PDFium/OCR는 TODO stub. 파일명·경로·크기·mtime은 저장되고 `Status = "indexed"`.
- 2026-08-18 (WPF): [WPF 인덱싱 버튼](e2cc63d9-6729-47b8-b1ef-2d47d4f0b6df)이 `MainWindow`에 **인덱싱**을 연결함. 폴더 선택만으로는 시작하지 않음. Core `IndexingService.Start` 호출 확인됨. 커밋하지 않음.
- Planner/사용자에게 수동 확인 요청: 폴더 없는 상태에서 버튼이 꺼져 있는지, 폴더 선택 후 **인덱싱**을 눌러 건수·현재 파일이 보이는지. 확인되면 이 두 항목을 완료로 표시해 주세요.

## Lessons

- 기능 검증은 배포 URL에서 수행 (웹). 이 작업은 로컬 .NET 라이브러리라 `dotnet test`가 검증이다.
- `dotnet` PATH가 비면 `C:\Program Files\dotnet\dotnet.exe`를 쓴다.
- 인덱스를 Velopack `current\`에 두면 업데이트 때 사라진다. UserData만 사용.
- Microsoft.Data.Sqlite 기본 연결 풀은 Windows에서 `index.db`를 잠근다. `Pooling=false` + `ClearAllPools()`가 필요하다.
