using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Seat> seats = new List<Seat>();
    public List<Card> deck = new List<Card>();      //현재 덱
    
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //기본 덱
    [SerializeField]
    private List<Transform> boardCardsPositions = new List<Transform>();

    private void Awake()
    {
        boardCardsPositions.Add(GameObject.Find("Table").transform.GetChild(10).transform);
        boardCardsPositions.Add(GameObject.Find("Table").transform.GetChild(11).transform);
        boardCardsPositions.Add(GameObject.Find("Table").transform.GetChild(12).transform);
        boardCardsPositions.Add(GameObject.Find("Table").transform.GetChild(13).transform);
        boardCardsPositions.Add(GameObject.Find("Table").transform.GetChild(14).transform);
    }
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }
    public void InitializeDeck()      //덱 초기화 및 생성
    {
        deck.Clear();
        deck.AddRange(originalDeck);
        Debug.Log("덱 생성");
    }

    public void ShuffleDeck()       //덱 셔플
    {
        InitializeDeck();       //덱 초기화

        Card[] allCardsInScene = FindObjectsOfType<Card>();     //씬 내 모든 카드
        for(int i =0; i< allCardsInScene.Length; i++)
        {
            Destroy(allCardsInScene[i].gameObject);             //씬 내 모든 카드 삭제
        }


        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            Card temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
        Debug.Log("덱 셔플 완료");
    }

    public void Preplop()       // 프리플랍
    {
        // 번 카드 한 장
        if (deck.Count > 0)
            deck.RemoveAt(0);

        // 좌석 리스트 초기화
        seats.Clear();

        // 모든 좌석 탐색
        GameObject[] allSeats = GameObject.FindGameObjectsWithTag("Seat");

        for (int i = 0; i < allSeats.Length; i++)
        {
            GameObject seatObj = allSeats[i];
            Seat seat = seatObj.GetComponent<Seat>();

            if (seat != null && seat.isSeated)
            {
                seats.Add(seat);
            }
            else
            {
                Debug.Log($"[Preflop] 착석중인 플레이어 X ({seatObj.name})");
            }
        }

        Debug.Log($"[Preflop] 현재 착석중인 좌석 수: {seats.Count}");

        if (deck.Count <= 0)
        {
            Debug.LogWarning("[Preflop] 덱에 카드가 없습니다.");
            return;
        }

        float cardOffset = 0.9f; // 카드 간격

        // 각 좌석에 2장씩 배분
        for (int i = 0; i < seats.Count; i++)
        {
            Seat seat = seats[i];
            if (seat == null) continue;

            // 플레이어 오브젝트 탐색 (자식으로 있다고 가정)
            Player player = seat.GetComponentInChildren<Player>(true);
            Transform parent = (player != null) ? player.transform : seat.transform;

            // 1️⃣ 첫 번째 카드
            if (deck.Count > 0)
            {
                Card newCard = Instantiate(deck[0], seat.transform.position, Quaternion.identity);
                newCard.transform.SetParent(parent, worldPositionStays: true);
                deck.RemoveAt(0);
            }

            // 2️⃣ 두 번째 카드
            if (deck.Count > 0)
            {
                Vector3 rightOffset = new Vector3(cardOffset, 0, 0);
                Vector3 spawnPos = seat.transform.position + rightOffset;

                Card newCard = Instantiate(deck[0], spawnPos, Quaternion.identity);
                newCard.transform.SetParent(parent, worldPositionStays: true);
                deck.RemoveAt(0);
            }
        }

        Debug.Log("[Preflop] 카드 배분 완료 (Player 자식으로 부착)");
    }

    public void Plop()          //플랍
    {
        deck.RemoveAt(0);       //카드 한장 번
        if (deck.Count > 0)
        {
            for(int i=0; i<3; i++)
            {
                Card newCard = Instantiate(deck[0], boardCardsPositions[i].position, Quaternion.identity);
                newCard.transform.SetParent(boardCardsPositions[i]);
                deck.RemoveAt(0);
            }
            
        }
        else
        {
            Debug.Log("덱에 카드가 없습니다.");
        }
    }
    public void Turn()          //턴
    {
        deck.RemoveAt(0);       //카드 한장 번
        if (deck.Count > 0)
        {
            Card newCard = Instantiate(deck[0], boardCardsPositions[3].position, Quaternion.identity);
            newCard.transform.SetParent(boardCardsPositions[3]);
            deck.RemoveAt(0);
        }
        else
        {
            Debug.Log("덱에 카드가 없습니다.");
        }

    }
    public void River()         //리버
    {
        deck.RemoveAt(0);       //카드 한장 번
        if (deck.Count > 0)
        {
            Card newCard = Instantiate(deck[0], boardCardsPositions[4].position, Quaternion.identity);
            newCard.transform.SetParent(boardCardsPositions[4]);
            deck.RemoveAt(0);
        }
        else
        {
            Debug.Log("덱에 카드가 없습니다.");
        }
    }
    public List<CardData> GetBoardCardData()
    {
        var result = new List<CardData>();
        foreach (var t in boardCardsPositions) // 너가 이미 갖고있는 Transform 리스트
        {
            var c = t.GetComponentInChildren<Card>();
            if (c != null) result.Add(c.cardData);
        }
        return result;
    }
}
