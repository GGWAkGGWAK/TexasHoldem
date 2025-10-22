using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Seat> seats = new List<Seat>();
    public List<Card> deck = new List<Card>();      //ÇöÀç µ¦
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //±âº» µ¦
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }
    public void InitializeDeck()      //µ¦ ÃÊ±âÈ­ ¹× »ý¼º
    {
        deck.Clear();
        deck.AddRange(originalDeck);
        Debug.Log("µ¦ »ý¼º");
    }

    public void ShuffleDeck()       //µ¦ ¼ÅÇÃ
    {
        InitializeDeck();
        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            Card temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
        Debug.Log("µ¦ ¼ÅÇÃ ¿Ï·á");
    }

    public void DrawingCard()       //Ä«µå³ª´²ÁÖ±â
    {
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
        }
        Debug.Log($"ÇöÀç Âø¼®ÁßÀÎ ÁÂ¼® ¼ö: {seats.Count}");
        if (deck.Count > 0)
        {
            for (int i = 0; i < seats.Count; i++)           //ÇÃ¶ø1
            {

                Debug.Log(deck[0]);
                deck.RemoveAt(0);
            }

            for (int i = 0; i < seats.Count; i++)           //ÇÃ¶ø2
            {

                Debug.Log(deck[0]);
                deck.RemoveAt(0);
            }
        }
        else
        {
            Debug.Log("Âø¼®ÁßÀÎ ÇÃ·¹ÀÌ¾î X");
        }
        
        
    }
}
