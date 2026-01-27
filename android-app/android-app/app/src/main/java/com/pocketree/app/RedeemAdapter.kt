package com.pocketree.app

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView


class RedeemAdapter(
    private val skins: List<Skin>,
    private val onItemClick: (Skin) -> Unit
) : RecyclerView.Adapter<RedeemAdapter.RedeemViewHolder>() {

    class RedeemViewHolder(view: View) : RecyclerView.ViewHolder(view) {
        val image: ImageView = view.findViewById(R.id.itemImage)
        val name: TextView = view.findViewById(R.id.itemName)
        val price: TextView = view.findViewById(R.id.itemPrice)
    }

    override fun onCreateViewHolder(parent: ViewGroup, viewType: Int): RedeemViewHolder {
        val view = LayoutInflater.from(parent.context)
            .inflate(R.layout.item_redeem, parent, false)
        return RedeemViewHolder(view)
    }

    override fun onBindViewHolder(holder: RedeemViewHolder, position: Int) {
        val skin = skins[position]
        holder.name.text = skin.name
        holder.price.text = "${skin.price} coins"
        // 这里设置图片，暂时使用默认图标
        // holder.image.setImageResource(item.imageResId)
        holder.image.setImageResource(android.R.drawable.ic_menu_gallery)
        holder.itemView.setOnClickListener {
            onItemClick(skin)
        }
    }

    override fun getItemCount() = skins.size
}