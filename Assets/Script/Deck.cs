using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Seat> seats = new List<Seat>();
    public List<Card> deck = new List<Card>();      //���� ��
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //�⺻ ��
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
    public void InitializeDeck()      //�� �ʱ�ȭ �� ����
    {
        deck.Clear();
        deck.AddRange(originalDeck);
        Debug.Log("�� ����");
    }

    public void ShuffleDeck()       //�� ����
    {
        InitializeDeck();       //�� �ʱ�ȭ

        Card[] allCardsInScene = FindObjectsOfType<Card>();     //�� �� ��� ī��
        for(int i =0; i< allCardsInScene.Length; i++)
        {
            Destroy(allCardsInScene[i].gameObject);             //�� �� ��� ī�� ����
        }


        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            Card temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
        Debug.Log("�� ���� �Ϸ�");
    }

    public void Preplop()       //�����ö�
    {
        deck.RemoveAt(0);   //ī������ ��

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
                Debug.Log("�������� �÷��̾� X");
            }
        }
        Debug.Log($"���� �������� �¼� ��: {seats.Count}");
        if (deck.Count > 0)
        {
            float cardOffset = 0.9f; //ī�尣 ����

            for (int i = 0; i < seats.Count; i++)           //�� �÷��̾��� ù��° ī��
            {
                Card newCard = Instantiate(deck[0], seats[i].transform.position, Quaternion.identity);
                newCard.transform.SetParent(seats[i].transform);

                deck.RemoveAt(0);
            }

            for (int i = 0; i < seats.Count; i++)           //�� �÷��̾��� �ι�° ī��
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
            Debug.Log("���� ī�尡 �����ϴ�.");
        }
        
        
    }
    public void Plop()          //�ö�
    {
        deck.RemoveAt(0);       //ī�� ���� ��
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
            Debug.Log("���� ī�尡 �����ϴ�.");
        }
    }
    public void Turn()          //��
    {
        deck.RemoveAt(0);       //ī�� ���� ��
        if (deck.Count > 0)
        {
            Card newCard = Instantiate(deck[0], boardCardsPositions[3].position, Quaternion.identity);
            newCard.transform.SetParent(boardCardsPositions[3]);
            deck.RemoveAt(0);
        }
        else
        {
            Debug.Log("���� ī�尡 �����ϴ�.");
        }

    }
    public void River()         //����
    {
        deck.RemoveAt(0);       //ī�� ���� ��
        if (deck.Count > 0)
        {
            Card newCard = Instantiate(deck[0], boardCardsPositions[4].position, Quaternion.identity);
            newCard.transform.SetParent(boardCardsPositions[4]);
            deck.RemoveAt(0);
        }
        else
        {
            Debug.Log("���� ī�尡 �����ϴ�.");
        }
    }
}
