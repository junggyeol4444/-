# MYVOCAL Studio — Windows 데스크톱 프로그램

MYVOCAL Studio는 웹사이트가 아니라 **네이티브 데스크톱 창으로 실행되는 음악·영상 제작 프로그램**입니다. Python 표준 GUI인 Tk/Tkinter로 작성했으며 브라우저나 로컬 웹 서버를 사용하지 않습니다.

## Windows EXE 만들기

Windows PC에서 저장소를 내려받고 `build_windows.bat`을 실행합니다.

```bat
build_windows.bat
```

완료 후 다음 파일이 생성됩니다.

```text
dist\MYVOCAL-Studio.exe
```

GitHub 저장소에서는 **Actions → Build Windows EXE → Run workflow**를 실행해도 `MYVOCAL-Studio-Windows` 아티팩트로 EXE를 받을 수 있습니다.

## 소스에서 바로 실행

Python 3.10 이상이 설치된 Windows, macOS 또는 Linux에서:

```bash
python main.py
```

별도의 웹 서버, Node.js 또는 브라우저는 필요하지 않습니다.

## 현재 동작하는 기능

- 네이티브 데스크톱 메뉴 및 프로젝트 창
- 멀티트랙 타임라인과 클립 드래그·스냅
- 재생 헤드, 재생·정지·되감기
- Vocal, Instrument, Audio, Video 트랙 추가
- 트랙 Mute/Solo 및 프로젝트 구간 표시
- 가사 편집과 AI 다시 쓰기
- Piano Roll, Automation, MV Storyboard 편집기
- AI Producer 자연어 명령과 타임라인 수정
- `.myvocal` 프로젝트 저장 및 다시 열기
- 실제 스테레오 WAV 데모 음원 렌더링 및 내보내기
- Windows 단일 실행 파일 패키징

## 프로젝트 파일

프로젝트는 JSON 기반의 `.myvocal` 파일로 저장합니다. 트랙, 클립, BPM과 편집 상태가 포함되며 이후 실제 보이스 모델 및 음악·영상 Provider를 연결할 수 있습니다.

> AI 생성과 영상 렌더링 Provider 연동은 아직 시뮬레이션입니다. 그러나 프로그램 자체, 프로젝트 저장/열기, 타임라인 편집, WAV 렌더링은 로컬 데스크톱에서 동작합니다.
