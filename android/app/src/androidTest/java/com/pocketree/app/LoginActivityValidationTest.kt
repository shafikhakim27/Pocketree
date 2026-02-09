package com.pocketree.app

import androidx.test.core.app.ActivityScenario
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.action.ViewActions.closeSoftKeyboard
import androidx.test.espresso.action.ViewActions.typeText
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class LoginActivityValidationTest {
    private fun clearAuthState() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        NetworkClient.setToken(context, null)
        context.getSharedPreferences("AppPrefs", android.content.Context.MODE_PRIVATE)
            .edit()
            .clear()
            .apply()
    }

    @Test
    fun emptyUsernameAndPassword_showsErrors() {
        clearAuthState()
        ActivityScenario.launch(LoginActivity::class.java).use {
            onView(withId(R.id.loginButton)).perform(click())

            onView(withId(R.id.usernameLayout))
                .check { view, _ ->
                    androidx.test.espresso.assertion.ViewAssertions.matches(
                        hasTextInputLayoutErrorText("Username is required")
                    ).check(view, null)
                }
            onView(withId(R.id.passwordLayout))
                .check { view, _ ->
                    androidx.test.espresso.assertion.ViewAssertions.matches(
                        hasTextInputLayoutErrorText("Password is required")
                    ).check(view, null)
                }
        }
    }

    @Test
    fun emptyPassword_showsPasswordError() {
        clearAuthState()
        ActivityScenario.launch(LoginActivity::class.java).use {
            onView(withId(R.id.username)).perform(typeText("user"), closeSoftKeyboard())
            onView(withId(R.id.loginButton)).perform(click())

            onView(withId(R.id.passwordLayout))
                .check { view, _ ->
                    androidx.test.espresso.assertion.ViewAssertions.matches(
                        hasTextInputLayoutErrorText("Password is required")
                    ).check(view, null)
                }
        }
    }

    @Test
    fun emptyUsername_showsUsernameError() {
        clearAuthState()
        ActivityScenario.launch(LoginActivity::class.java).use {
            onView(withId(R.id.password)).perform(typeText("pass"), closeSoftKeyboard())
            onView(withId(R.id.loginButton)).perform(click())

            onView(withId(R.id.usernameLayout))
                .check { view, _ ->
                    androidx.test.espresso.assertion.ViewAssertions.matches(
                        hasTextInputLayoutErrorText("Username is required")
                    ).check(view, null)
                }
        }
    }
}
