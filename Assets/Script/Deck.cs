using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<int> deck = new List<int>();
    void Start()
    {

    }


    public void ShuffleDeck()
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
