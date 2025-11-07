using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Seat> seats = new List<Seat>();
    public List<Card> deck = new List<Card>(); // 현재 덱

    [SerializeField] private List<Card> originalDeck = new List<Card>(); // 기본 52장(프리팹/프리셋)
    [SerializeField] private List<Transform> boardCardsPositions = new List<Transform>();

    private void Awake()
    {
        var table = GameObject.Find("Table").transform;
        boardCardsPositions.Add(table.GetChild(10));
        boardCardsPositions.Add(table.GetChild(11));
        boardCardsPositions.Add(table.GetChild(12));
        boardCardsPositions.Add(table.GetChild(13));
        boardCardsPositions.Add(table.GetChild(14));
    }

    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }

    public void InitializeDeck()
    {
        deck.Clear();
        deck.AddRange(originalDeck);
    }

    public void ShuffleDeck()
    {
        InitializeDeck();

        // 씬에 남아있는 카드 삭제
        Card[] allCardsInScene = FindObjectsOfType<Card>();
        for (int i = 0; i < allCardsInScene.Length; i++)
            Destroy(allCardsInScene[i].gameObject);

        // Fisher–Yates
        for (int i = 0; i < deck.Count; i++)
        {
            int r = Random.Range(i, deck.Count);
            var tmp = deck[i];
            deck[i] = deck[r];
            deck[r] = tmp;
        }

        Debug.Log("덱 셔플 완료");
    }

    //SB → … → BTN 순서로 두 장씩 배분 (버튼이 마지막으로 받음)
    public void PreflopDealInOrder(List<Seat> order)
    {
        if (order == null || order.Count == 0)
        {
            Debug.LogWarning("Preflop order empty");
            return;
        }

        float cardOffset = 0.9f;

        // 첫 바퀴 (각자 첫 장)
        for (int i = 0; i < order.Count; i++)
        {
            var seat = order[i];
            if (seat == null || !seat.isSeated) continue;

            var player = seat.GetComponentInChildren<Player>(true);
            Transform parent = (player != null) ? player.transform : seat.transform;

            if (deck.Count > 0)
            {
                Card c = Instantiate(deck[0], seat.transform.position, Quaternion.identity);
                c.transform.SetParent(parent, true);
                deck.RemoveAt(0);
            }
        }

        // 두 번째 바퀴 (오른쪽 오프셋)
        for (int i = 0; i < order.Count; i++)
        {
            var seat = order[i];
            if (seat == null || !seat.isSeated) continue;

            var player = seat.GetComponentInChildren<Player>(true);
            Transform parent = (player != null) ? player.transform : seat.transform;

            if (deck.Count > 0)
            {
                Vector3 spawnPos = seat.transform.position + new Vector3(cardOffset, 0, 0);
                Card c = Instantiate(deck[0], spawnPos, Quaternion.identity);
                c.transform.SetParent(parent, true);
                deck.RemoveAt(0);
            }
        }
    }

    public void Plop()
    {
        if (deck.Count > 0) deck.RemoveAt(0); // 번
        for (int i = 0; i < 3; i++)
        {
            if (deck.Count == 0) break;
            Card c = Instantiate(deck[0], boardCardsPositions[i].position, Quaternion.identity);
            c.transform.SetParent(boardCardsPositions[i], true);
            deck.RemoveAt(0);
        }
    }

    public void Turn()
    {
        if (deck.Count > 0) deck.RemoveAt(0); // 번
        if (deck.Count > 0)
        {
            Card c = Instantiate(deck[0], boardCardsPositions[3].position, Quaternion.identity);
            c.transform.SetParent(boardCardsPositions[3], true);
            deck.RemoveAt(0);
        }
    }

    public void River()
    {
        if (deck.Count > 0) deck.RemoveAt(0); // 번
        if (deck.Count > 0)
        {
            Card c = Instantiate(deck[0], boardCardsPositions[4].position, Quaternion.identity);
            c.transform.SetParent(boardCardsPositions[4], true);
            deck.RemoveAt(0);
        }
    }

    public List<CardData> GetBoardCardData()
    {
        var result = new List<CardData>();
        foreach (var t in boardCardsPositions)
        {
            var c = t.GetComponentInChildren<Card>();
            if (c != null) result.Add(c.cardData);
        }
        return result;
    }
}
