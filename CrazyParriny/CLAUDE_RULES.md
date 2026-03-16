# CrazyParrying 코드 작성 규칙

## 최우선 적용사항
- 1순위 사항으로 최적화, 가독성 고려해서 작성
- return new AAA / yield return new AAA 같은거 금지 미리 캐싱할 것
- destory , dotween kill같은거 꼭써야하는거 아니면 사용 금지 최적화 고려 필수
- 모르는건 물어보고 답변 받은 다음 구현할 것
- 추가 변수/설정 같은 것 꼭 필요한거 아니면 무제한 사용 금지 특히 static [현재 이상하고 불필요한거 계속 생산 중]
- 코드 간결하고 인스펙터 설정이 가능하면 그쪽으로 돌릴것 요약정리를 통해 해당 부분 알릴 것
- 추가 생산이 아닌 기존 코드 수정은 꼭 요약/핵심정리를 통해 확실히 할것
- 필요하고 사용가능 한 것만 구현할 것
- 중복은 제거
- 작성 후 버그/오류 검사 할 것
## static 사용 기준
- **허용**: 여러 인스턴스가 진짜 공유해야 하는 데이터 (LayerMask, NonAlloc 버퍼)
- **금지**: 인스턴스가 하나뿐인 클래스(Player, InGameManager 등)의 내부 상태
- **금지**: `GetComponent` 결과, 계산 결과를 굳이 static으로 캐싱하는 것 — 대안이 있으면 제거

## 변수
- private static: `s_` 접두사
- private instance: camelCase (접두사 없음)
- SerializeField: 인스펙터에서 조정할 값에만 사용, 코드에서만 쓰는 값은 일반 필드

## 함수
- 한 함수 = 한 역할
- 단순 반환은 expression body(`=>`)
- null 체크/방어 코드는 시스템 경계(외부 입력, 풀에서 꺼낸 직후)에만

## 초기화
- 캐싱 bool(`isCached`, `isInit`)은 꼭 필요할 때만 — `Physics.IgnoreLayerCollision`처럼 1회성 설정에만 허용
- LayerMask.GetMask는 Awake에서 직접 할당, 별도 bool 없이

## 주석
- "왜"가 불명확할 때만 작성
- "무엇을 하는지"는 코드 자체로 표현

## 기타
- 이미 기능하는 코드에 방어 코드/fallback 추가 금지
- 사용하지 않는 변수, 주석 처리된 코드 즉시 제거
- 풀링 오브젝트: Awake에서 컴포넌트 캐싱, OnSpawn/Launch에서 상태 초기화
- 
