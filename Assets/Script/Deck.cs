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

    public void Preplop()       //프리플랍
    {
        deck.RemoveAt(0);   //카드한장 번

        seats.Clear();

        GameObject[] allSeats = GameObject.FindGameObjectsWithTag("Seat");

        for(int i = 0; i< allSeats.Length; i++)
        {
            GameObject seatObj = allSeats[i];
            Seat seat = seatObj.GetComponent<Seat>();

            if(seat!=null && seat.isSeated)
            {
                seats.Add(seat);
            }
            else
            {
                Debug.Log("착석중인 플레이어 X");
            }
        }
        Debug.Log($"현재 착석중인 좌석 수: {seats.Count}");
        if (deck.Count > 0)
        {
            float cardOffset = 0.9f; //카드간 간격

            for (int i = 0; i < seats.Count; i++)           //각 플레이어의 첫번째 카드
            {
                Card newCard = Instantiate(deck[0], seats[i].transform.position, Quaternion.identity);
                newCard.transform.SetParent(seats[i].transform);

                deck.RemoveAt(0);
            }

            for (int i = 0; i < seats.Count; i++)           //각 플레이어의 두번째 카드
            {
                Vector3 rightOffset = new Vector3(cardOffset, 0, 0);
                Vector3 spawnPos = seats[i].transform.position + rightOffset;

                Card newCard = Instantiate(deck[0], spawnPos, Quaternion.identity);
                newCard.transform.SetParent(seats[i].transform);

                deck.RemoveAt(0);
            }
        }
        else
        {
            Debug.Log("덱에 카드가 없습니다.");
        }
        
        
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
}
