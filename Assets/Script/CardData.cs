using UnityEngine;

[System.Serializable]
public struct CardData
{
    public int cardShape;   // 0: �����̵�, 1: ���̾Ƹ��, 2: ��Ʈ, 3: Ŭ�ι�
    public int cardNumber;  // 2 ~ 14(11 J, 12 Q, 13 K, 14 A)
    public Sprite cardImage;

    public CardData(int shape, int number, Sprite image)
    {
        cardShape = shape;
        cardNumber = number;
        cardImage = image;
    }
}
