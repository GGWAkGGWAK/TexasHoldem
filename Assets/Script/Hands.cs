using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hands : MonoBehaviour
{
    public int handLevel;           //하이 0, 원페어 1, 투페어 2, 트리플 3, 스트레이트 4, 플러시 5, 풀하우스 6, 포카드 7, 스트레이트 플러시 8, 로얄 스트레이트 플러시 9
    public List<Card> playerHand = new List<Card>();


    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
