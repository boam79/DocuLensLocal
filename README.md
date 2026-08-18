# DocuLens Local

Windows 10/11용 설치형 PDF 검색기입니다. 계약서·MOU 같은 PDF를 **원래 폴더에 그대로 둔 채** 인덱싱하고, 파일명·본문·날짜로 찾습니다. 문서 원문은 외부 서버로 보내지 않습니다.

- 대상: Windows 10/11 64비트
- 스택: .NET 10, WPF, 로컬 SQLite
- 저장소: https://github.com/boam79/DocuLensLocal

## 설치 파일 받기

Windows 64비트용 설치 파일입니다. .NET을 따로 설치하지 않아도 됩니다.

- 릴리스 목록: https://github.com/boam79/DocuLensLocal/releases
- 최신 설치 파일: https://github.com/boam79/DocuLensLocal/releases/latest
- 설치 실행 파일 이름: `DocuLensLocal-win-Setup.exe`

받은 `DocuLensLocal-win-Setup.exe`를 실행하면 설치됩니다. 코드 서명이 없어 Windows가 경고를 띄울 수 있습니다. 추가 정보를 연 뒤 실행을 선택하면 됩니다.

업데이트도 같은 Releases 경로를 사용합니다. PDF와 검색 인덱스는 전송하지 않습니다.

다른 컴퓨터에 쓰려면 그 컴퓨터에도 이 설치 파일을 받아 설치하고, 그 PC에서 열 수 있는 PDF 폴더를 선택하세요. 폴더 이름이 `계약서`가 아니어도 됩니다.

## 최초 실행

등록된 폴더가 없으면 다음 안내가 나옵니다.

- 제목: 인덱싱할 폴더를 선택하세요
- 본문: 검색할 PDF가 들어 있는 폴더를 선택하세요. 이 컴퓨터의 폴더 이름은 ‘계약서’가 아니어도 됩니다.
- 보조: 다른 컴퓨터에서는 폴더 이름과 위치가 다를 수 있습니다. 그 컴퓨터에 있는 PDF 폴더를 새로 선택하면 됩니다.

글자가 들어 있는 PDF는 OCR하지 않고 기존 텍스트만 읽습니다. 스캔(이미지) 페이지만 백그라운드에서 OCR합니다. 사용자는 페이지가 하나씩 뜨는 화면을 보지 않습니다.

## 개발용으로 실행

필요 환경: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

```powershell
git clone https://github.com/boam79/DocuLensLocal.git
cd DocuLensLocal
dotnet test
dotnet build
dotnet run --project src/DocuLensLocal.App
```

솔루션 파일은 `DocuLensLocal.slnx` 입니다.

| 프로젝트 | 역할 |
|---|---|
| `src/DocuLensLocal.App` | WPF 화면 |
| `src/DocuLensLocal.Worker` | 인덱싱·OCR 백그라운드 |
| `src/DocuLensLocal.Core` | 공통 로직 |
| `tests/DocuLensLocal.Core.Tests` | 테스트 |

검색 인덱스와 모델은 `%LOCALAPPDATA%\DocuLensLocal`에 저장할 예정입니다. 원본 PDF는 읽기만 합니다.

설치 파일을 이 저장소에서 다시 만들려면:

```powershell
.\scripts\pack.ps1
```

결과는 `artifacts\Releases\DocuLensLocal-win-Setup.exe` 입니다.
