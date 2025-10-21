using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    public CardData cardData;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rigid;
    
    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }
    void Update()
    {
        cardData.cardImage = gameObject.transform.GetChild(0).gameObject.GetComponent<SpriteRenderer>().sprite;
    }
    public void Initialize(CardData data)
    {
        cardData = data;
        spriteRenderer.sprite = data.cardImage;
    }
}
