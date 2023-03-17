using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public enum CardColor { Red, Green, Yellow, Blue, Wild };
public class Card : MonoBehaviour
{
    /*
        1-10 are regular (1, 2, 3... 0)
        11 is draw 2
        12 is skip
        13 is reverse
        14 is wild
        15 is wild draw 4
    */

    [SerializeField] int number;
    [SerializeField] CardColor color;

    public bool canPlay;

    private SpriteRenderer cardSprite;
    private BoxCollider2D boxCollider;
    private static Sprite[] cardSpriteSheet;

    private float ZRotation = 0f;

    void Start()
    {
        boxCollider = GetComponent<BoxCollider2D>();
        cardSprite = GetComponent<SpriteRenderer>();

        boxCollider.size = new Vector2(cardSprite.size.x, cardSprite.size.y);
        boxCollider.offset = new Vector2(cardSprite.size.x/2, 0);

        cardSpriteSheet = Resources.LoadAll<Sprite>("Graphics/Cards");

        if (transform.parent.name == "Cards")
        {

            switch (int.Parse(transform.parent.parent.name.Split(' ').Last())) // We get the index of the player, i.e 1 of "player 1"
            {
                case 2:
                    ZRotation = -90f;
                    break;
                case 3:
                    ZRotation = -180f;
                    break;
                case 4:
                    ZRotation = -270f;
                    break;
            }
            transform.Rotate(new Vector3(0, 0, ZRotation));
        }

        UpdateTexture();

        /*
            Texture convention - corresponds to cards sprite sheet

            1 - wild
            6 - wild draw 4
            12-24 - yellows - numbers; draw 2; skip; reverse
            25-37 - reds - numbers; draw 2; skip; reverse
            38-50 - blues - numbers; draw 2; skip; reverse
            51-63 - greens - numbers; draw 2; skip; reverse
         */
    }

    public int GetNumber() { return this.number; }
    public CardColor GetColor() { return this.color; }
    public void SetNumber(int newNumber)
    {
        this.number = newNumber;
    }
    public void SetColor(CardColor newColor)
    {
        this.color = newColor;
    }
    public void ColorMutator(CardColor newColor)
    { 
        //mutator that changes the color of a wild card to make the color noticeable
        if (GetNumber() == 14)
        {
            this.color = newColor;
            switch (this.color)
            {
                case CardColor.Yellow:
                    cardSprite.sprite = cardSpriteSheet[2];
                    break;
                case CardColor.Red:
                    cardSprite.sprite = cardSpriteSheet[3];
                    break;
                case CardColor.Blue:
                    cardSprite.sprite = cardSpriteSheet[4];
                    break;
                case CardColor.Green:
                    cardSprite.sprite = cardSpriteSheet[5];
                    break;
            }
        }
        else if (GetNumber() == 15)
        {
            this.color = newColor;
            switch (this.color)
            {
                case CardColor.Yellow:
                    cardSprite.sprite = cardSpriteSheet[7];
                    break;
                case CardColor.Red:
                    cardSprite.sprite = cardSpriteSheet[8];
                    break;
                case CardColor.Blue:
                    cardSprite.sprite = cardSpriteSheet[9];
                    break;
                case CardColor.Green:
                    cardSprite.sprite = cardSpriteSheet[10];
                    break;
            }
        }
        else
        {
            Debug.Log("Error: card isn't wild, cannot change color");
        }
    }

    //private void OnMouseDown()
    //{
    //    if (potentialToPlay && owner.GetCanPlay() && canPlay)
    //    {
    //        PlayCard();
    //    }
    //}

    public void UpdateTexture()
    {
        int spritePathNum = 0;
        switch (color)
        {
            case CardColor.Red:
            { spritePathNum = 24 + number; break; }
            case CardColor.Green:
            { spritePathNum = 50 + number; break; }
            case CardColor.Yellow:
            { spritePathNum = 11 + number; break; }
            case CardColor.Blue:
            { spritePathNum = 37 + number; break; }
            case CardColor.Wild:
            {
                switch (number)
                {
                    case 14:
                    { spritePathNum = 1; break; }
                    case 15:
                    { spritePathNum = 6; break; }
                    default:
                    { spritePathNum = 0; break; }
                }
                break;
            }
        }
        cardSprite.sprite = cardSpriteSheet[spritePathNum];
    }

    public void DestroyCard() { Destroy(gameObject); }
}