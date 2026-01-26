package com.pocketree.app

import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.ImageView
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView


class RedeemAdapter(
    private val items: List<RedeemItem>,
    private val onItemClick: (RedeemItem) -> Unit
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
        val item = items[position]
        holder.name.text = item.name
        holder.price.text = "${item.price} Coins"
        // 这里设置图片，暂时使用默认图标
        // holder.image.setImageResource(item.imageResId)
        holder.image.setImageResource(android.R.drawable.ic_menu_gallery)
        holder.itemView.setOnClickListener {
            onItemClick(item)
        }
    }

    override fun getItemCount() = items.size
}