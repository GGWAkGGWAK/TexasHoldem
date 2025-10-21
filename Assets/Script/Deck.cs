using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<int> deck = new List<int>();
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }

    public void InitializeDeck()      //µ¦ »ý¼º
    {
        deck.Clear();
        for (int i = 0; i < 52; i++)
        {
            deck.Add(i);
        }
    }

    public void ShuffleDeck()       //µ¦ ¼ÅÇÃ
    {
        deck.Clear();
        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            int temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
    }

    public void DrawingCard()
    {
        
    }
}
