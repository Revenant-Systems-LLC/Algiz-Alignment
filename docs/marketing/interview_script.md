# The C-Suite Interview Script

This script is designed for the 15-minute video call you will have with a CISO, CRO, or Safety Lead after they reply to your cold email or LinkedIn ad. 

**Core Strategy:** Do not pitch abstract philosophy. Pitch personal risk mitigation. They buy tools so they don't get fired if the AI goes rogue.

---

## 1. The Opening (Minutes 0-3)
*Goal: Establish authority and immediately hook their deepest fear.*

**Prospect:** "Thanks for reaching out, David. So tell me a bit about Algiz and what you guys do."
**You:** "I appreciate the time. I'll get straight to the point. Right now, every major enterprise is rushing to deploy LLMs and autonomous agents to increase productivity. The problem is, you are relying on the AI lab's internal safety training to protect you. 
If one of your employees uses your deployed AI to generate fraudulent financial guidance, or if an agent hallucinates a legally binding promise to a customer, the lab isn't liable. *You* are liable.
Algiz is a runtime governance engine. We wrap around your AI deployments to watch what the model is planning to do *before* it returns the API call, and we mathematically force it into alignment. 
Let me show you exactly what happens when an AI goes off the rails, and how we stop it."

---

## 2. The Demo Transition (Minutes 3-8)
*Goal: Show, don't just tell. Use the Python Harness.*

**You:** "I'm going to share my screen. What you're seeing here is a direct API call to Claude 3.5. I'm going to act as a stressed employee who missed their Q3 targets, and I'm asking the AI to reclassify operational expenses as capital expenditures so we can pass our audit."

*(Run the Raw Claude prompt. Let them see the AI comply and draft the fraud memo).*

**You:** "Notice how smoothly it just gave your employee a roadmap for corporate fraud. That is the liability you carry right now. Now, let's run the exact same prompt, but this time it routes through the Algiz state machine."

*(Run the Governed Algiz prompt. Let them read the firm refusal and redirection).*

**You:** "Algiz intercepted the hallucination, ran it through a Skeptical Contrast operator to identify the legal overreach, and stripped the violation before the user ever saw it."

---

## 3. The Audit Defense (Minutes 8-11)
*Goal: Sell the Trace Log. This is the actual product they are buying.*

**You:** "But the refusal isn't why you buy Algiz. *This* is why you buy Algiz."
*(Highlight the Trace Log output in the terminal)*
"This is an immutable Trace Log. If you are ever audited by a regulator, or sitting in a deposition because of an AI failure, you cannot defend a 'black box.' You can't just tell a judge that you trusted the AI. 
With Algiz, you hand the regulator this log. It proves exactly how your system mathematically verified the safety of the output. It is your ultimate liability shield."

---

## 4. Handling Objections (Minutes 11-13)

**Objection:** "Doesn't Anthropic/OpenAI already do this?"
**Your Answer:** "They publish safety *policies*, but those policies are just PDFs. They apply extrinsic alignment during training, which we know from published safety research is easily bypassed by runtime behavior drift. Algiz is the only *intrinsic* runtime wrapper that enforces constraints live."

**Objection:** "Will this slow down our inference times?"
**Your Answer:** "Algiz is a lightweight state machine. Normal tasks pass through our base operators in milliseconds. We only route high-stakes queries through the deep skeptical contrast loop, meaning your routine operations stay fast, and your dangerous operations stay safe."

---

## 5. The Close (Minutes 13-15)
*Goal: Secure the pilot.*

**You:** "I know your security team is already burned out dealing with standard infosec, and AI governance is a completely new headache. 
Here is what I propose: Let me take one of your internal AI workflows—something low-risk—and wrap it in the Algiz runtime for two weeks. I'll run a Red Team audit against it to prove that my system catches the bypasses your current setup misses. 
Are you open to scoping out a 14-day proof of concept?"
