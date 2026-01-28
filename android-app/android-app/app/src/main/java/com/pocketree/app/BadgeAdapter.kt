package com.pocketree.app

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.pocketree.app.databinding.ItemBadgeBinding

class BadgeAdapter (
    private val badges:List<Badge>
): RecyclerView.Adapter<BadgeAdapter.BadgeViewHolder>(){
    class BadgeViewHolder(val binding: ItemBadgeBinding): RecyclerView.ViewHolder(binding.root)

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): BadgeViewHolder {
        val binding = ItemBadgeBinding.inflate(LayoutInflater.from(parent.context), parent, false)
        return BadgeViewHolder(binding)
    }

    override fun onBindViewHolder(holder:BadgeViewHolder, position:Int) {
        val badge = badges[position]
        holder.binding.badgeName.text = badge.badgeName

        val iconRes = when(badge.badgeName) {
            "Sapling Badge" -> R.drawable.redeem_item_1 // example for now
            "Oak Badge" -> R.drawable.redeem_item_1

            "Easy Master Badge" -> R.drawable.redeem_item_2
            "Normal Hero Badge" -> R.drawable.redeem_item_2
            "Hard Legend Badge" -> R.drawable.redeem_item_2

            else -> R.drawable.redeem_item_3
        }
        holder.binding.badgeImage.setImageResource(iconRes)
    }

    override fun getItemCount() = badges.size
}