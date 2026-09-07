# Nextorial_Project
2026 넥토리얼 인턴쉽 채용 관련

장르 & 컨셉)
  2.5D SideView Action RPG
  유저와 파트너 드래곤과 함께 전투를 통해 성장하는 RPG

Scene 구성)
  BootstrapScene -> LobbyScene -> GameScene

리소스 관리)
  ScriptableObject를 통해 캐릭터 내 스텟 관리
  Addressables를 사용하여 Resource Load 관리
  ObjectPooling 방식을 통해 이펙트 및 연출 처리
  JsonUtility 기반 로컬 데이터 저장 및 로드 

Scene 흐름 구조)
  <img width="1070" height="615" alt="캡처_2026_09_07_14_04_58_21" src="https://github.com/user-attachments/assets/49c77af0-8b12-4cad-b54e-3262ba5f207c" />
  BootstrapScene의 SaveDataController.Load()로 세이브 유무 확인
  SequenceManager를 통해 SceneLoad 및 Addressable	초기화·사운드	초기화 세	단계를 순서대로 실행
  재사용 가능 유틸리티 형식으로 설계

Code Folder 구조)
  <img width="1109" height="687" alt="캡처_2026_09_07_14_16_05_821" src="https://github.com/user-attachments/assets/4c0ae832-cf48-45ee-ac8d-71aeee8a5566" />

공용 전투 파이프라인 구조)
  <img width="1070" height="615" alt="캡처_2026_09_07_14_04_58_21" src="https://github.com/user-attachments/assets/da92b564-91fb-44d0-9848-be38c7d08408" />




