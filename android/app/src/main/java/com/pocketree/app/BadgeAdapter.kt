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
        holder.binding.badgeName.text = badge.badgeName ?: "Badge"

        val context = holder.binding.root.context

        // format badge name eg "Mighty Oak" -> "mighty_oak_badge"
        val safeName = badge.badgeName?.lowercase()?.replace(" ", "_") ?: ""
        val imageName = if (safeName.isNotEmpty()) "${safeName}_badge" else ""

        // generate resource ID dynamically from the name
        val resourceId = if (imageName.isNotEmpty()) {
            context.resources.getIdentifier(imageName, "drawable", context.packageName)
        } else 0
        // safety net if fetching from cloud doesn't work - will try to fetch image from drawable

        val imageSource = when {
            !badge.badgeImageUrl.isNullOrEmpty() -> badge.badgeImageUrl
            resourceId != 0 -> resourceId
            else -> R.drawable.redeem_item_1
        }

        Glide.with(context)
            .load(imageSource)
            .placeholder(R.drawable.redeem_item_1) // usage of drawable image as placeholder is okay
            .error(R.drawable.redeem_item_1)
            .into(holder.binding.badgeImage)
    }
    override fun getItemCount() = badges.size
}