using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Card> deck = new List<Card>();      //ÇöÀç µ¦
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //±âº» µ¦
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }
    public void InitializeDeck()      //µ¦ »ý¼º
    {
        deck.Clear();
        deck = originalDeck;
        Debug.Log("µ¦ »ý¼º");
    }

    public void ShuffleDeck()       //µ¦ ¼ÅÇÃ
    {
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
        
    }
}
