package com.pocketree.app

import android.view.LayoutInflater
import android.view.ViewGroup
import androidx.recyclerview.widget.RecyclerView
import com.bumptech.glide.Glide
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

//        val iconRes = when(badge.badgeName) {
//            "Tree Starter Badge" -> R.drawable.redeem_item_1 // example for now
//            "Mighty Oak Badge" -> R.drawable.redeem_item_1
//
//            "Green Starter Badge" -> R.drawable.redeem_item_2
//            "Green Champion Badge" -> R.drawable.redeem_item_2
//            "Eco Warrior Badge" -> R.drawable.redeem_item_2
//
//            else -> R.drawable.redeem_item_3
//        }
//        holder.binding.badgeImage.setImageResource(iconRes)

        // format name to match drawable naming convention
        // eg "Mighty Oak" -> "mighty_oak_badge"
        val imageName = badge.badgeName.lowercase().replace(" ", "_") + "_badge"

        // get resource ID dynamically from the name
        val context = holder.binding.root.context
        val resourceId = context.resources.getIdentifier(imageName, "drawable", context.packageName)

        // use Glide to load the image
        Glide.with(context)
            .load(if (resourceId != 0) resourceId else R.drawable.redeem_item_3) // fallback if not found
            .placeholder(R.drawable.redeem_item_3)
            .into(holder.binding.badgeImage)
    }

    override fun getItemCount() = badges.size
}