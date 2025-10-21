using UnityEngine;

[System.Serializable]
public struct CardData
{
    public int cardShape;   // 0: 스페이드, 1: 다이아몬드, 2: 하트, 3: 클로버
    public int cardNumber;  // 2 ~ 14(11 J, 12 Q, 13 K, 14 A)
    public Sprite cardImage;

    public CardData(int shape, int number, Sprite image)
    {
        cardShape = shape;
        cardNumber = number;
        cardImage = image;
    }
}
